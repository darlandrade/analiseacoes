using Microsoft.Data.Sqlite;
using ScottPlot;
using ScottPlot.WinForms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography.Xml;
using System.Windows.Forms;
using WFColor = System.Drawing.Color;
using WFLabel = System.Windows.Forms.Label;

// ... seus usings

namespace AnaliseAcoes
{
    public partial class Form1 : Form
    {
        private ComboBox cmbCodigo;
        private Button btnBuscar, btnAtualizar;
        private WFLabel lblStatus;
        private FormsPlot grafico;

        public Form1()
        {
            try
            {
                InitializeComponent();
                InicializarComponentesCustom();
                var banco = new BancoSQLite();
                banco.CriarTabela();

                CarregarTickers(); // 🔹 carrega os tickers na inicialização
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao inicializar banco de dados: " + ex.Message);
            }
        }

        private void InicializarComponentesCustom()
        {
            this.Text = "Análise de Ações - EMA 21 e EMA 9";
            this.Size = new Size(1000, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = WFColor.FromArgb(40, 40, 40);

            cmbCodigo = new ComboBox
            {
                Location = new Point(20, 20),
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };

            btnBuscar = new Button { Text = "Buscar", Location = new Point(180, 18), Width = 80, ForeColor = WFColor.White };
            btnAtualizar = new Button { Text = "Atualizar Banco", Location = new Point(800, 18), Width = 120, ForeColor = WFColor.White };
            lblStatus = new WFLabel { Location = new Point(280, 22), AutoSize = true, ForeColor = WFColor.LightGray };
            grafico = new FormsPlot { Location = new Point(20, 60), Size = new Size(940, 480) };

            btnBuscar.Click += BtnBuscar_Click;
            btnAtualizar.Click += (s, e) => BtnAtualizar_Click();

            this.Controls.Add(cmbCodigo);
            this.Controls.Add(btnBuscar);
            this.Controls.Add(btnAtualizar);
            this.Controls.Add(lblStatus);
            this.Controls.Add(grafico);
        }
        private void CarregarTickers()
        {
            try
            {
                cmbCodigo.Items.Clear();

                var banco = new BancoSQLite();
                var tickers = banco.ObterTickers();

                if (tickers.Count > 0)
                {
                    cmbCodigo.Items.AddRange(tickers.ToArray());
                    lblStatus.Text = $"{tickers.Count} tickers carregados.";
                }
                else
                {
                    lblStatus.Text = "Nenhum ticker encontrado no banco.";
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Erro ao carregar tickers: " + ex.Message;
            }
        }

        private void BtnBuscar_Click(object sender, EventArgs e)
        {
            string codigo = cmbCodigo.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(codigo))
            {
                MessageBox.Show("Selecione ou digite o código da ação.");
                return;
            }

            lblStatus.Text = "Carregando dados...";
            btnBuscar.Enabled = false;

            try
            {
                var banco = new BancoSQLite();
                var dados = banco.ObterCotacoesSQLite(codigo, 21);

                if (dados.Count == 0)
                {
                    MessageBox.Show("Não foram encontrados dados para este ativo.");
                    return;
                }

                var ema21 = CalcularEMA(dados, 21);
                var ema9 = CalcularEMA(dados, 9);

                ExibirGrafico(dados, ema21, ema9, codigo);

                double ultimoPreco = dados[^1].Close;
                double ultimaEMA21 = ema21[^1];
                double ultimaEMA9 = ema9[^1];
                string sinal = ultimoPreco > ultimaEMA21 ? "📈 COMPRA" :
                               ultimoPreco < ultimaEMA21 ? "📉 VENDA" : "🔹 NEUTRO";

                lblStatus.Text = $"Último preço: {ultimoPreco:F2} | EMA21: {ultimaEMA21:F2} | EMA9: {ultimaEMA9:F2} → Sinal: {sinal}";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro: " + ex.Message);
            }
            finally
            {
                btnBuscar.Enabled = true;
            }
        }

        private void BtnAtualizar_Click()
        {
            btnAtualizar.Enabled = false;
            lblStatus.Text = "Atualizando banco...";
            var banco = new BancoSQLite();

            banco.CriarTabela();

            string caminhoArquivo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dados", "ativos.txt");

            if (File.Exists(caminhoArquivo))
            {
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dados", "cotacoes.db")}");
                conn.Open();

                var transaction = conn.BeginTransaction();
                using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;

                cmd.CommandText = @"
                    INSERT INTO cotacoes (ticker, data, preco_fechamento)
                    VALUES (@ticker, @data, @preco);";

                var pTicker = cmd.Parameters.Add("@ticker", Microsoft.Data.Sqlite.SqliteType.Text);
                var pData = cmd.Parameters.Add("@data", Microsoft.Data.Sqlite.SqliteType.Text);
                var pPreco = cmd.Parameters.Add("@preco", Microsoft.Data.Sqlite.SqliteType.Real);

                int contador = 0;
                foreach (var linha in File.ReadLines(caminhoArquivo))
                {
                    if (string.IsNullOrWhiteSpace(linha) || linha.Length < 120)
                        continue;

                    try
                    {
                        string ticker = linha.Substring(12, 12).Trim();
                        string dataStr = linha.Substring(2, 8);
                        string precoStr = linha.Substring(109, 12).Trim();

                        if (DateTime.TryParseExact(dataStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime data) &&
                            double.TryParse(precoStr, out double preco))
                        {
                            preco /= 100.0;

                            pTicker.Value = ticker;
                            pData.Value = data.ToString("yyyy-MM-dd");
                            pPreco.Value = preco;

                            cmd.ExecuteNonQuery();

                            contador++;
                            if (contador % 1000 == 0)
                            {
                                // Commit parcial a cada 1000 registros
                                transaction.Commit();
                                transaction.Dispose();
                                transaction = conn.BeginTransaction();
                                cmd.Transaction = transaction;
                            }
                        }
                    }
                    catch
                    {
                        // Ignorar linha mal formatada
                    }
                }

                transaction.Commit();
                conn.Close();
            }
            else
            {
                MessageBox.Show("Arquivo ativos.txt não encontrado em: " + caminhoArquivo);
            }

            var tickers = banco.ObterTickers();
            if (tickers.Count > 0)
                cmbCodigo.Items.AddRange(tickers.ToArray());
            else
                MessageBox.Show("Nenhum ticker encontrado no banco.");

            lblStatus.Text = "Banco atualizado.";
        }
        
        private List<double> CalcularEMA(List<Cotacao> dados, int periodo)
        {
            var ema = new List<double>();
            double k = 2.0 / (periodo + 1);
            double? anterior = null;

            foreach (var d in dados)
            {
                anterior = anterior == null ? d.Close : (d.Close - anterior.Value) * k + anterior.Value;
                ema.Add(anterior.Value);
            }

            return ema;
        }

        private void ExibirGrafico(List<Cotacao> dados, List<double> ema21, List<double> ema9, string codigo)
        {
            grafico.Plot.Clear();

            double[] xs = dados.Select(d => d.Data.ToOADate()).ToArray();
            double[] ys = dados.Select(d => d.Close).ToArray();
            double[] yEma21 = ema21.ToArray();
            double[] yEma9 = ema9.ToArray();

            var precoPlot = grafico.Plot.Add.Scatter(xs, ys);
            precoPlot.Color = new ScottPlot.Color(50, 205, 50);
            precoPlot.LegendText = "Preço";

            var ema21Plot = grafico.Plot.Add.Scatter(xs, yEma21);
            ema21Plot.Color = new ScottPlot.Color(255, 165, 0); // Laranja
            ema21Plot.LegendText = "EMA 21";

            var ema9Plot = grafico.Plot.Add.Scatter(xs, yEma9);
            ema9Plot.Color = new ScottPlot.Color(0, 191, 255); // Azul claro
            ema9Plot.LegendText = "EMA 9";

            grafico.Plot.Axes.DateTimeTicksBottom();
            grafico.Plot.Add.Legend();
            grafico.Plot.Title($"Análise de {codigo}");

            grafico.Refresh();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // nada aqui por enquanto
        }
    }


    public class Cotacao
    {
        public DateTime Data { get; set; }
        public double Close { get; set; }
    }
}
