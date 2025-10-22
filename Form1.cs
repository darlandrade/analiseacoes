using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ScottPlot;
using ScottPlot.WinForms;

using WFColor = System.Drawing.Color;
using WFLabel = System.Windows.Forms.Label;

namespace AnaliseAcoes
{
    public partial class Form1 : Form
    {
        private ComboBox cmbCodigo;
        private Button btnBuscar;
        private Button btnAtualizar;
        private WFLabel lblStatus;
        private FormsPlot grafico;

        public Form1()
        {
            InitializeComponent();
            InicializarComponentesCustom();

            var banco = new BancoSQLite();
            banco.CriarTabela();

            // Popula o ComboBox ao iniciar
            CarregarTickers();
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
            btnAtualizar = new Button { Text = "Atualizar Banco", Location = new Point(280, 18), Width = 120, ForeColor = WFColor.White };

            lblStatus = new WFLabel { Location = new Point(420, 22), AutoSize = true, ForeColor = WFColor.LightGray };

            grafico = new FormsPlot { Location = new Point(20, 60), Size = new Size(940, 480) };

            btnBuscar.Click += BtnBuscar_Click;
            btnAtualizar.Click += BtnAtualizar_Click;

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

                // Expressão regular: 3–4 letras/dígitos + 1–2 números (ex.: B3SA3, PETR4, ITUB11)
                var regex = new System.Text.RegularExpressions.Regex(@"^[A-Z0-9]{3,4}\d{1}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var tickersFiltrados = tickers
                    .Select(t => t.Trim().ToUpper())
                    .Where(t => regex.IsMatch(t))
                    .Distinct()
                    .OrderBy(t => t)
                    .ToList();
                cmbCodigo.Items.Clear();
                if (tickers.Count > 0)
                {
                    cmbCodigo.Items.AddRange(tickersFiltrados.ToArray());
                    //lblStatus.Text = $"{tickersFiltrados.Count} tickers carregados.";
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

        private void BtnAtualizar_Click(object sender, EventArgs e)
        {
            btnAtualizar.Enabled = false;
            lblStatus.Text = "Atualizando banco...";

            // Tela de loading
            Form loadingForm = new Form
            {
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ControlBox = false,
                Text = "Importando dados...",
                Size = new Size(400, 80)
            };
            ProgressBar progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 100,
                Style = ProgressBarStyle.Continuous
            };
            loadingForm.Controls.Add(progressBar);
            loadingForm.Show();

            Task.Run(() =>
            {
                try
                {
                    var banco = new BancoSQLite();
                    banco.CriarTabela();

                    string caminhoArquivo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dados", "ativos.txt");
                    if (!File.Exists(caminhoArquivo))
                    {
                        loadingForm.Invoke(new Action(() => MessageBox.Show("Arquivo ativos.txt não encontrado!")));
                        return;
                    }

                    var linhas = File.ReadLines(caminhoArquivo).ToList();
                    int totalLinhas = linhas.Count;
                    int contador = 0;

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

                    for (int i = 0; i < totalLinhas; i++)
                    {
                        string linha = linhas[i];
                        if (string.IsNullOrWhiteSpace(linha) || linha.Length < 120) continue;

                        try
                        {
                            string ticker = linha.Substring(12, Math.Min(12, linha.Length - 12)).Trim();
                            string dataStr = linha.Substring(2, 8);
                            string precoStr = linha.Substring(109, 12).Trim();

                            if (DateTime.TryParseExact(dataStr, "yyyyMMdd", null, DateTimeStyles.None, out DateTime data) &&
                                double.TryParse(precoStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double preco))
                            {
                                preco /= 100.0;

                                pTicker.Value = ticker;
                                pData.Value = data.ToString("yyyy-MM-dd");
                                pPreco.Value = preco;

                                cmd.ExecuteNonQuery();
                                contador++;
                            }
                        }
                        catch { }

                        // Commit parcial a cada 1000 registros
                        if (contador % 1000 == 0)
                        {
                            transaction.Commit();
                            transaction.Dispose();
                            transaction = conn.BeginTransaction();
                            cmd.Transaction = transaction;
                        }

                        // Atualiza barra de progresso a cada 500 linhas
                        if (i % 500 == 0)
                        {
                            int progresso = (i + 1) * 100 / totalLinhas;
                            loadingForm.Invoke(new Action(() => progressBar.Value = progresso));
                        }
                    }

                    transaction.Commit();
                    conn.Close();

                    // Fecha loading e atualiza UI
                    loadingForm.Invoke(new Action(() =>
                    {
                        loadingForm.Close();
                        lblStatus.Text = "Banco atualizado.";
                        MessageBox.Show($"Importação concluída! {contador} registros inseridos.");
                        CarregarTickers();
                    }));
                }
                catch (Exception ex)
                {
                    loadingForm.Invoke(new Action(() =>
                    {
                        loadingForm.Close();
                        MessageBox.Show("Erro ao atualizar banco: " + ex.Message);
                        lblStatus.Text = "Erro ao atualizar.";
                    }));
                }
                finally
                {
                    this.Invoke(new Action(() => btnAtualizar.Enabled = true));
                }
            });
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
            ema21Plot.Color = new ScottPlot.Color(255, 165, 0);
            ema21Plot.LegendText = "EMA 21";

            var ema9Plot = grafico.Plot.Add.Scatter(xs, yEma9);
            ema9Plot.Color = new ScottPlot.Color(0, 191, 255);
            ema9Plot.LegendText = "EMA 9";

            grafico.Plot.Axes.DateTimeTicksBottom();
            grafico.Plot.Add.Legend();
            grafico.Plot.Title($"Análise de {codigo}");

            grafico.Refresh();
        }
    }

    public class Cotacao
    {
        public DateTime Data { get; set; }
        public double Close { get; set; }
    }
}
