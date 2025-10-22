using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using ScottPlot;
using ScottPlot.WinForms;
using YahooFinanceApi;

using WFColor = System.Drawing.Color;
using WFLabel = System.Windows.Forms.Label;
using System.Security.Cryptography.X509Certificates;



namespace AnaliseAcoes
{
    public partial class Form1 : Form
    {       
        //Cores customizadas
        WFColor OVERSELECTION = WFColor.FromArgb(34, 16, 82);
        WFColor CORHEADERSBACK = WFColor.FromArgb(70, 70, 70);
        WFColor CORFORE = WFColor.White;


        private ComboBox cmbCodigo;
        private Button btnBuscar;
        private Button btnAtualizar, btnAdicionar, btnAtualizarPreco, btnRemover;
        private WFLabel lblStatus, lblTotalInvestido, lblGanhoPerda, lblSaldo, lblGanhoPerdaValor;
        private FormsPlot grafico;
        private DataGridView grid;

        private List<AcaoInfo> acoes = new List<AcaoInfo>();
        private readonly string caminhoArquivo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dados", "acoes.json");

        public Form1()
        {
            InitializeComponent();
            InicializarGraficoAnalise();

            this.Width = 1910;
            this.Height = 1000;

            InicializarCompraEVenda();
            AtualizarGrid(new BDInfoGrid());
            CarregarDadosDoArquivo();
            AtualizarStatusLabels(new BDInfoGrid { Acoes = acoes });
            CarregarSaldo();

            var banco = new BancoSQLite();
            banco.CriarTabela();

            // Popula o ComboBox ao iniciar
            CarregarTickers();
        }

        // ========================================
        // Inicializa a seção de Compra e Venda
        // ========================================
        private void InicializarCompraEVenda()
        {
            Panel pnCompraVenda = new Panel
            {
                Location = new Point(970, 10),
                Size = new Size(920, 600)
            };
            this.Controls.Add(pnCompraVenda);

            // Painel para botões
            Panel pnBotoes = new Panel
            {
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(10, 5),
                Size = new Size(900, 50)
            };
            pnCompraVenda.Controls.Add(pnBotoes);

            string[] btnNomes = { "Comprar", "Vender", "Atualizar Preços", "Add Saldo", "Google Finance" };
            foreach (var nome in btnNomes)
            {
                Button botao = CriarBotao(nome);
                botao.Location = new Point(pnBotoes.Controls.Count * (botao.Width + 10) + 10, 10);

                pnBotoes.Controls.Add(botao);

                // click event para abrir google finance
                if (nome == "Google Finance")
                {
                    botao.Click += (s, e) =>
                    {
                        if (grid.SelectedRows.Count > 0)
                        {
                            string ticker = grid.SelectedRows[0].Cells["Ticker"].Value.ToString();
                            AbrirGoogleFinance(ticker);
                        }
                        else
                        {
                            MessageBox.Show("Selecione uma ação no grid para abrir no Google Finance.");
                        }
                    };
                }
                if (nome == "Add Saldo")
                {
                    botao.Click += (s, e) =>
                    {
                        AdicionarSaldo();
                    };
                }
            }

            // Painel para labels de status
            FlowLayoutPanel pnStatus = new FlowLayoutPanel
            {
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(10, 65),
                Size = new Size(900, 30),
                FlowDirection = FlowDirection.LeftToRight,
                //Margin = new Padding(100, 3, 100, 3),
                AutoSize = true
            };
            pnCompraVenda.Controls.Add(pnStatus);

            string[] lblNomes = { "Total Investido: ", "Ganho/Perda: ","0", "Saldo: " };
            for (int i = 0; i < lblNomes.Length; i++)
            {
                WFLabel label = CriarLabel(lblNomes[i]);

                // Salva referência e define margem específica
                switch (i)
                {
                    case 0:
                        lblTotalInvestido = label;
                        label.Margin = new Padding(10, 5, 120, 5);
                        break;
                    case 1:
                        lblGanhoPerda = label;
                        label.Margin = new Padding(10, 5, 0, 5); // margem direita zero para colar no próximo
                        break;
                    case 2:
                        lblGanhoPerdaValor = label;
                        label.Margin = new Padding(0, 5, 120, 5); // margem esquerda zero para colar no anterior
                        break;
                    case 3:
                        lblSaldo = label;
                        label.Margin = new Padding(10, 5, 10, 5);
                        break;
                }

                pnStatus.Controls.Add(label);
            }
            // DataGridView para exibir compras e vendas
            grid = new DataGridView
            {
                Location = new Point(10, 100), // Posição abaixo dos botões e labels
                Size = new Size(900, 430), // Tamanho para preencher o restante do painel
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, // Ajusta colunas para preencher o espaço
                BackgroundColor = WFColor.FromArgb(50, 50, 50), 
                ForeColor = WFColor.LightGray,
                EnableHeadersVisualStyles= false, // Desabilita estilos visuais padrão
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = CORHEADERSBACK,
                    ForeColor = CORFORE,
                    Font = new System.Drawing.Font("Consolas", 9),
                    SelectionBackColor = CORHEADERSBACK,
                    SelectionForeColor = CORFORE,
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }, // Estilo do cabeçalho

                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = WFColor.FromArgb(50, 50, 50),
                    ForeColor = WFColor.LightGray,
                    Font = new System.Drawing.Font("Consolas", 12 ),
                    SelectionBackColor = OVERSELECTION,
                    SelectionForeColor = CORFORE,
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }, // Estilo das células

                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                AllowUserToResizeColumns = false,                
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, // Seleciona a linha inteira
                GridColor = WFColor.Gray, // Cor das linhas do grid

                // Headers estilizados
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,

                };

            

            pnCompraVenda.Controls.Add(grid);
            ConfigurarGrid();

        }
        // Cria uma nova janela para adicionar saldo
        private void AdicionarSaldo()
        {
            Form formAdicionarSaldo = new Form
            {
                Text = "Adicionar Saldo",
                Size = new Size(300, 150),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };
            WFLabel lblValor = new WFLabel
            {
                Text = "Valor a adicionar:",
                Location = new Point(10, 20),
                AutoSize = true
            };
            formAdicionarSaldo.Controls.Add(lblValor);
            TextBox txtValor = new TextBox
            {
                Location = new Point(10, 50),
                Width = 260
            };
            formAdicionarSaldo.Controls.Add(txtValor);
            Button btnOk = new Button
            {
                Text = "OK",
                Location = new Point(200, 80),
                DialogResult = DialogResult.OK
            };
            formAdicionarSaldo.Controls.Add(btnOk);
            formAdicionarSaldo.AcceptButton = btnOk;
            if (formAdicionarSaldo.ShowDialog() == DialogResult.OK)
            {
                if (decimal.TryParse(txtValor.Text, NumberStyles.Currency, CultureInfo.CurrentCulture, out decimal valor))
                {
                    // Lógica para adicionar saldo
                    btnCarregarSaldo(valor);
                }
                else
                {
                    MessageBox.Show("Valor inválido. Tente novamente.");
                }
            }
        }
        // Adiciona saldo
        public class SaldoInfo
        {
            public decimal Budget { get; set; }

        }
        // Para adicionar no inicio para carregar o saldo no app
        private void CarregarSaldo()
        {
            // Lê o arquivo saldo.json
            string caminhoSaldo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dados", "saldo.json");
            decimal saldoAtual = 0;
            if (File.Exists(caminhoSaldo))
            {
                try
                {
                    string json = File.ReadAllText(caminhoSaldo);
                    SaldoInfo info = JsonSerializer.Deserialize<SaldoInfo>(json);
                    saldoAtual = info?.Budget ?? 0;
                    // Atualiza o label de saldo
                    lblSaldo.Text = $"Saldo: {saldoAtual.ToString("C2", new CultureInfo("pt-BR"))}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erro ao ler saldo: " + ex.Message);
                }
            }
        }
        // Para usar no botão adicionar saldo
        private void btnCarregarSaldo(decimal valor)
        {
            
            // Lê o arquivo saldo.json
            string caminhoSaldo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dados", "saldo.json");
            decimal saldoAtual = 0;            

            if (File.Exists(caminhoSaldo))
            {
                try
                {
                    string json = File.ReadAllText(caminhoSaldo);
                    SaldoInfo info = JsonSerializer.Deserialize<SaldoInfo>(json);
                    saldoAtual = info?.Budget ?? 0;
                    // Adiciona o valor
                    saldoAtual += valor;
                    // Salva o novo saldo
                    info = new SaldoInfo { Budget = saldoAtual };
                    string novoJson = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(caminhoSaldo, novoJson);

                    // Atualiza o label de saldo
                    lblSaldo.Text = $"Saldo: {saldoAtual.ToString("C2", new CultureInfo("pt-BR"))}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erro ao ler saldo: " + ex.Message);
                }
            }
        }

        // Abre google finance no navegador
        private void AbrirGoogleFinance(string ticker)
        {
            MessageBox.Show("Abrindo Google Finance para: " + ticker);
            string url = $"https://www.google.com/finance/quote/{ticker}:BVMF";
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao abrir o navegador: " + ex.Message);
            }
        }

        // Configuração do grid
        private void ConfigurarGrid()
        {
            grid.Columns.Clear();
            grid.Columns.Add("Ticker", "Ticker");
            grid.Columns.Add("Quantidade", "Quantidade");
            grid.Columns.Add("PrecoMedio", "Preço Médio");
            grid.Columns.Add("PrecoAtual", "Preço Atual");
            grid.Columns.Add("ValorAtual", "Valor Atual");
            grid.Columns.Add("StopLoss", "Stop Loss");
            grid.Columns.Add("Alvo", "Alvo");
            grid.Columns.Add("Valorizacao", "Valorização");
            grid.Columns.Add("Data", "Data");
            foreach (DataGridViewColumn coluna in grid.Columns)
            {
                coluna.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }  
        }
        // Carrega dados do arquivo acoes.JSON
        private void CarregarDadosDoArquivo()
        {
            if (File.Exists(caminhoArquivo))
            {
                try
                {
                    string json = File.ReadAllText(caminhoArquivo);
                    acoes = JsonSerializer.Deserialize<List<AcaoInfo>>(json) ?? new List<AcaoInfo>();
                    AtualizarGrid(new BDInfoGrid { Acoes = acoes });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erro ao carregar dados: " + ex.Message);
                }
            }
        }
        // Atualizar o grid
        private void AtualizarGrid(BDInfoGrid bdInfo)
        {
            grid.Rows.Clear();
            foreach (var acao in bdInfo.Acoes)
            {
                grid.Rows.Add(acao.Ticker, acao.Quantidade, acao.PrecoMedio.ToString("F2"),
                              acao.PrecoAtual.ToString("F2"), acao.ValorAtual.ToString("F2"),
                              acao.StopLoss.ToString("F2"), acao.Alvo.ToString("F2"),
                              acao.Valorizacao.ToString("P2"), acao.Data.ToString("yyyy-MM-dd"));
            }
        }

        // Atualizar status labels
        private void AtualizarStatusLabels(BDInfoGrid bdInfo)
        {
            
            decimal totalInvestido = bdInfo.Acoes.Sum(a => a.PrecoMedio * a.Quantidade);
            decimal valorAtual = bdInfo.Acoes.Sum(a => a.ValorAtual);
            decimal ganhoPerda = valorAtual - totalInvestido;
            decimal saldo = bdInfo.Saldo;
            if (lblTotalInvestido == null)
            {
                MessageBox.Show("lblTotalInvestido está nulo!");
                return;
            }
            lblTotalInvestido.Text = $"Total Investido: {totalInvestido.ToString("C2", new CultureInfo("pt-BR"))}";
            lblGanhoPerda.Text = $"Ganho/Perda: ";
            lblGanhoPerdaValor.Text = $"{ganhoPerda.ToString("C2", new CultureInfo("pt-BR"))} " +
                                      $"({(totalInvestido == 0 ? 0 : ganhoPerda / totalInvestido).ToString("P2", new CultureInfo("pt-BR"))})";

            if (ganhoPerda >= 0)
            {
                lblGanhoPerdaValor.ForeColor = WFColor.LightGreen;
            }
            else
            {
                lblGanhoPerdaValor.ForeColor = WFColor.Red;
            }
            lblSaldo.Text = $"Saldo: {saldo.ToString("C2", new CultureInfo("pt-BR"))}";          

        }
        // Campos para pesquisa no JSON
        private class AcaoInfo
        {
            public string Ticker { get; set; }
            public int Quantidade { get; set; }
            public decimal PrecoMedio { get; set; }
            public decimal PrecoAtual { get; set; }
            public decimal ValorAtual { get; set; }
            public decimal StopLoss { get; set; }
            public decimal Alvo { get; set; }
            public decimal Valorizacao { get; set; }
            public DateTime Data { get; set; }
        }
        // Estrutura do banco de dados para o DataGridView
        private class BDInfoGrid
        {
            public List<AcaoInfo> Acoes { get; set; } = new List<AcaoInfo>();
            public decimal Saldo { get; set; } = 0;
        }
        private Button CriarBotao(string texto)
        {
            return new Button
            {
                Text = texto,
                Width = 150,
                Height = 30,
                Margin = new Padding(10, 10, 0, 0),
                ForeColor = CORFORE,
                BackColor = WFColor.FromArgb(70, 70, 70),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0, MouseDownBackColor = WFColor.FromArgb(49, 30, 102), MouseOverBackColor = OVERSELECTION }
                
            };
        }
        // Helper para criar labels
        private WFLabel CriarLabel(string texto)
        {
            return new WFLabel
            {
                Text = texto,
                AutoSize = true,
                Margin = new Padding(0, 0, 20, 0),
                ForeColor = WFColor.LightGray
            };
        }
        // ========================================
        // Inicializa Gráfico de analise de ações
        // ========================================
        private void InicializarGraficoAnalise()
        {
            this.Text = "Análise de Ações - EMA 21 e EMA 9";
            //this.Size = new Size(1000, 600);
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

            btnBuscar = CriarBotao("Buscar");
            btnBuscar.Location = new Point(180, 18);
            btnBuscar.Width = 80;
            btnAtualizar = CriarBotao("Atualizar Banco");
            btnAtualizar.Location = new Point(280, 18);
            btnAtualizar.Width = 120;

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
                    cmbCodigo.SelectedIndex = 0;
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
