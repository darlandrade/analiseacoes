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

namespace AnaliseAcoes
{
    public partial class Form1 : Form
    {
        private ComboBox cmbCodigo;
        private Button btnBuscar;
        private WFLabel lblStatus;
        private FormsPlot grafico;

        public Form1()
        {
            InitializeComponent();
            InicializarComponentesCustom();

            var banco = new BancoSQLite();
            banco.CriarTabela();

            foreach (var linha in File.ReadLines("ativos.txt"))
            {
                string ticker = linha.Substring(12, 12).Trim();
                string dataStr = linha.Substring(2, 8);
                string precoStr = linha.Substring(109, 12).Trim();

                if (DateTime.TryParseExact(dataStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime data) &&
                    double.TryParse(precoStr, out double preco))
                {
                    preco /= 100.0;
                    banco.InserirCotacao(ticker, data, preco);
                }
            }
        }

        private void InicializarComponentesCustom()
        {
            this.Text = "Análise de Ações - EMA 21 e EMA 9";
            this.Size = new Size(1000, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = WFColor.FromArgb(40, 40, 40);

            cmbCodigo = new ComboBox { Location = new Point(20, 20), Width = 100, 
                DropDownStyle = ComboBoxStyle.DropDown, 
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems};
            btnBuscar = new Button { Text = "Buscar", Location = new Point(130, 18), Width = 80, ForeColor = WFColor.White };
            lblStatus = new WFLabel { Location = new Point(230, 22), AutoSize = true, ForeColor = WFColor.LightGray };

            grafico = new FormsPlot { Location = new Point(20, 60), Size = new Size(940, 480) };

            btnBuscar.Click += BtnBuscar_Click;

            this.Controls.Add(cmbCodigo);
            this.Controls.Add(btnBuscar);
            this.Controls.Add(lblStatus);
            this.Controls.Add(grafico);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string caminhoArquivo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dados", "ativos.txt");

            if (File.Exists(caminhoArquivo))
            {
                var tickers = File.ReadLines(caminhoArquivo)
                    .Select(l => l.Substring(12, 12).Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Distinct()
                    .OrderBy(t => t)
                    .ToArray();

                cmbCodigo.Items.AddRange(tickers);
            }
            else
            {
                MessageBox.Show("Arquivo ativos.txt não encontrado.");
            }

        }

        private void BtnBuscar_Click(object sender, EventArgs e)
        {
            //string codigo = cmbCodigo.Text.Trim().ToUpper();
            string codigo = cmbCodigo.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(codigo))
            {
                MessageBox.Show("Digite o código da ação (ex: PETR4, B3SA3)");
                return;
            }

            lblStatus.Text = "Carregando dados...";
            btnBuscar.Enabled = false;

            try
            {
                string caminhoArquivo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dados", "ativos.txt");

                var dados = ObterDadosB3(caminhoArquivo, codigo);

                if (dados.Count == 0)
                {
                    MessageBox.Show("Não foram encontrados dados para este ativo.");
                    return;
                }

                // Calcula EMA21 e EMA9
                var ema21 = CalcularEMA(dados, 21);
                var ema9 = CalcularEMA(dados, 9);

                // Exibe gráfico
                ExibirGrafico(dados, ema21, ema9, codigo);

                // Últimos valores e sinal
                double ultimoPreco = dados[^1].Close;
                double ultimaEMA21 = ema21[^1];
                double ultimaEMA9 = ema9[^1];
                string sinal = ultimoPreco > ultimaEMA21 ? "📈 COMPRA" :
                               ultimoPreco < ultimaEMA21 ? "📉 VENDA" : "🔹 NEUTRO";

                lblStatus.Text = $"Carregando últimos {dados.Count} dias de {codigo}. Último preço: {ultimoPreco:F2} | EMA21: {ultimaEMA21:F2} | EMA9: {ultimaEMA9:F2} → Sinal: {sinal}";
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

        private List<Cotacao> ObterDadosB3(string caminhoArquivo, string codigo)
        {
            var lista = new List<Cotacao>();

            foreach (var linha in File.ReadLines(caminhoArquivo))
            {   

                if (string.IsNullOrWhiteSpace(linha)) continue;

                try
                {
                    // Data da negociação: posições 2 a 9 (8 caracteres)
                    string dataStr = linha.Substring(2, 8);
                    if (!DateTime.TryParseExact(dataStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime data))
                        continue;

                    // Código do ativo: posições 10 a 21 (12 caracteres)
                    string codigoAtivo = linha.Substring(12, 12).Trim();


                    if (!codigoAtivo.Equals(codigo, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Preço de fechamento: posições 108 a 119 (12 caracteres)
                    string precoStr = linha.Substring(109, 12).Trim();
                    if (double.TryParse(precoStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double close))
                    {
                        close /= 100.0; // ajustar para valor real
                        lista.Add(new Cotacao { Data = data, Close = close });
                    }
                }
                catch
                {
                    // ignora linhas mal formatadas
                    continue;
                }
            }
            DateTime referencia = new DateTime(2025,10,16);
            DateTime limite = referencia.AddDays(-21);

            return lista
                .Where(c => c.Data >= limite && c.Data <= referencia)
                .OrderBy(c => c.Data)
                .ToList();
                    }

        private List<double> CalcularEMA(List<Cotacao> dados, int periodo)
        {
            var ema = new List<double>();
            double k = 2.0 / (periodo + 1);
            double? anterior = null;

            foreach (var d in dados)
            {
                if (anterior == null)
                    anterior = d.Close;
                else
                    anterior = (d.Close - anterior.Value) * k + anterior.Value;

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
            precoPlot.Label = "Preço";

            var ema21Plot = grafico.Plot.Add.Scatter(xs, yEma21);
            ema21Plot.Color = new ScottPlot.Color(255, 165, 0);
            ema21Plot.Label = "EMA 21";

            var ema9Plot = grafico.Plot.Add.Scatter(xs, yEma9);
            ema9Plot.Color = new ScottPlot.Color(0, 191, 255);
            ema9Plot.Label = "EMA 9";

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
