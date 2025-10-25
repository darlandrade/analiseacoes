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
using AnaliseAcoes;

using WFColor = System.Drawing.Color;
using WFLabel = System.Windows.Forms.Label;
using WFFont = System.Drawing.Font;
using System.Security.Cryptography.X509Certificates;


namespace AnaliseAcoes
{
    public partial class Form1 : Form
    {       
        //Cores customizadas
        public static WFColor OVERSELECTION = WFColor.FromArgb(112,21,12); // Cor Hover do botão
        public static WFColor MOUSEDOWNCOLOR = WFColor.FromArgb(120, 36, 29); // Cor quando o botão é clicado
        WFColor CORHEADERSBACK = WFColor.FromArgb(70, 70, 70); // Cor de fundo dos headers do grid
        public static WFColor CORFORE = WFColor.White;
        public static WFColor BACKGROUND = WFColor.FromArgb(40, 40, 40);
        public static WFFont FONTEPADRAO(string nome="Consolas", int t = 9)
        {
            return new Font(nome, t);

        }

        public enum TipoOperacao
        {
            Compra,
            Venda
        }

        private Panel pnBotoes, pnLiquida;
        private Panel pnLSLVermelho, pnAVerde;

        private ComboBox cmbCodigo;
        private Button btnBuscar;
        private Button btnAtualizar, btnLiquida;
        private WFLabel lblStatus, lblTotalInvestido, lblGanhoPerda, lblSaldo, lblGanhoPerdaValor, lblValorHover;
        private WFLabel lbLegendaStopLoss, lbLgendaAlvo;
        private FormsPlot grafico;
        private DataGridView grid;

        private decimal tInvestidoGeral = 0;

        private List<AcaoInfo> acoes = new List<AcaoInfo>();
        private List<string> tickersFiltrados = new();
        private readonly string caminhoArquivo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dados", "acoes.json");

        // dentro da classe Form1
        private double[] currentXs = Array.Empty<double>();
        private double[] currentYs = Array.Empty<double>();


        public Form1()
        {
            InitializeComponent();
            InicializarGraficoAnalise();
            this.BackColor = BACKGROUND;
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
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(970, 10),
                Size = new Size(920, 540)
            };
            this.Controls.Add(pnCompraVenda);

            // Painel para botões
            pnBotoes = new Panel
            {
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(10, 5),
                Size = new Size(900, 50)
            };
            pnCompraVenda.Controls.Add(pnBotoes);

            CriarBotoesPainel();

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
                        label.Margin = new Padding(20, 5, 100, 5);
                        break;
                    case 1:
                        lblGanhoPerda = label;
                        label.Margin = new Padding(10, 5, 0, 5); // margem direita zero para colar no próximo
                        break;
                    case 2:
                        lblGanhoPerdaValor = label;
                        label.Margin = new Padding(0, 5, 100, 5); // margem esquerda zero para colar no anterior
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
                    Font = FONTEPADRAO(),
                    SelectionBackColor = CORHEADERSBACK,
                    SelectionForeColor = CORFORE,
                    Alignment = DataGridViewContentAlignment.MiddleCenter,                                        
                }, // Estilo do cabeçalho

                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = WFColor.FromArgb(50, 50, 50),
                    ForeColor = WFColor.LightGray,
                    Font = FONTEPADRAO(t:12),
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

            grid.CellPainting += dataGridView1_CellPainting;
            grid.ShowCellToolTips = false;
            pnCompraVenda.Controls.Add(grid);
            ConfigurarGrid();

            // Liquidação
            pnLiquida = new Panel
            {
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(970, 550),
                Size = new Size(920, 40)
            };
            this.Controls.Add(pnLiquida);
            btnLiquida = CriarBotao("Liquidação");
            btnLiquida.Location = new Point(10, 5);
            pnLiquida.Controls.Add(btnLiquida);

            btnLiquida.Click += (s, e) => liquidarAcoes();

            pnLSLVermelho = new Panel
            {
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(btnLiquida.Width + 20, 5),
                Size = new Size(30, 30),
                BackColor = WFColor.FromArgb(117, 57, 57)
            };
            pnLiquida.Controls.Add(pnLSLVermelho);
            lbLegendaStopLoss = CriarLabel("Stop Loss Atingido");   
            lbLegendaStopLoss.Location = new Point(pnLSLVermelho.Location.X + pnLSLVermelho.Width + 5, 10);
            pnLiquida.Controls.Add(lbLegendaStopLoss);

            pnAVerde = new Panel
            {
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(lbLegendaStopLoss.Location.X + lbLegendaStopLoss.Width + 20, 5),
                Size = new Size(30, 30),
                BackColor = WFColor.FromArgb(67, 117, 57)
            };
            pnLiquida.Controls.Add(pnAVerde);
            lbLgendaAlvo = CriarLabel("Alvo Atingido");
            lbLgendaAlvo.Location = new Point(pnAVerde.Location.X + pnAVerde.Width + 5, 10);
            pnLiquida.Controls.Add(lbLgendaAlvo);
        }

        // Função para liquidar ações
        private void liquidarAcoes()
        {
            string ticker = grid.SelectedRows[0].Cells["Ticker"].Value.ToString();
            int quantidade = Convert.ToInt32(grid.SelectedRows[0].Cells["Quantidade"].Value);
            decimal vAtual = Convert.ToDecimal(grid.SelectedRows[0].Cells["PrecoAtual"].Value);
            decimal pMedio = Convert.ToDecimal(grid.SelectedRows[0].Cells["PrecoMedio"].Value);
            int inicio = ticker.IndexOf('(');
            int fim = ticker.IndexOf(')');

            string comprado = (inicio >= 0 && fim > inicio)
                ? ticker.Substring(inicio + 1, fim - inicio - 1)
                : "";

            bool estaComprado = comprado == "C";
            var form = new FormLiquidaAcoes(ticker.Substring(0, 5), quantidade, tInvestidoGeral, pMedio, vAtual, estaComprado, acoes);
            form.AoFechar += () =>
            {
                CarregarDadosDoArquivo();
                AtualizarGrid(new BDInfoGrid { Acoes = acoes });
                AtualizarStatusLabels(new BDInfoGrid { Acoes = acoes });
                CarregarSaldo();
            };
            form.ShowDialog();

        }

        // Cria os botões com ações associadas
        private void CriarBotoesPainel()
        {
            var acoesBotoes = new Dictionary<string, EventHandler>
            {
                ["Comprar"] = (s, e) => ExecutarOperacao(TipoOperacao.Compra),
                ["Vender"] = (s, e) => ExecutarOperacao(TipoOperacao.Venda),
                ["Atualizar Preços"] = async (s, e) => await AtualizarValoresAsync(),
                ["Add Saldo"] = (s, e) => AdicionarSaldo(),
                ["Google Finance"] = (s, e) => AbrirGoogleFinanceSelecionado()
            };

            int x = 10;
            foreach (var (nome, acao) in acoesBotoes)
            {
                var botao = CriarBotao(nome);
                botao.Location = new Point(x, 10);
                botao.Click += acao;

                pnBotoes.Controls.Add(botao);
                x += botao.Width + 10;
            }
        }
        private void ExecutarOperacao(TipoOperacao tipo)
        {
            var form = new FormCompraVenda(tipo, tickersFiltrados, cmbCodigo.Text);
            if (form.ShowDialog() == DialogResult.OK)
            {
                CarregarDadosDoArquivo();
                AtualizarGrid(new BDInfoGrid { Acoes = acoes });
                AtualizarStatusLabels(new BDInfoGrid { Acoes = acoes });
                CarregarSaldo();
            }
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
            //MessageBox.Show("Abrindo Google Finance para: " + ticker);
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

        private void AbrirGoogleFinanceSelecionado()
        {
            if (grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Selecione uma ação no grid para abrir no Google Finance.");
                return;
            }

            string ticker = grid.SelectedRows[0].Cells["Ticker"].Value.ToString();
            AbrirGoogleFinance(ticker.Substring(0, 5));
        }

        // Configuração do grid
        private void ConfigurarGrid()
        {
            grid.Columns.Clear();
            grid.Columns.Add("Ticker", "Ticker");
            grid.Columns.Add("Quantidade", "Quantidade");
            grid.Columns.Add("PrecoMedio", "Preço Médio");
            grid.Columns.Add("PrecoAtual", "Preço Atual");
            grid.Columns.Add("TotalInvestido", "Investido");
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
                acao.TotalInvestido = acao.PrecoMedio * acao.Quantidade;
                bool estaComprado = acao.Operacao == "C";

                acao.Valorizacao = estaComprado
                    ? (acao.PrecoAtual - acao.PrecoMedio) / acao.PrecoMedio
                    : (acao.PrecoMedio - acao.PrecoAtual) / acao.PrecoMedio;

                int idx = grid.Rows.Add(
                    acao.Ticker + " (" + acao.Operacao + ")",
                    acao.Quantidade,
                    acao.PrecoMedio.ToString("F2"),
                    acao.PrecoAtual.ToString("F2"),
                    acao.TotalInvestido.ToString("F2"),
                    acao.StopLoss.ToString("F2"),
                    acao.Alvo.ToString("F2"),
                    acao.Valorizacao.ToString("P2"),
                    acao.Data.ToString("yyyy-MM-dd")
                );

                var row = grid.Rows[idx];

                // ✅ Lógica de cor
                if (estaComprado)
                    if (acao.PrecoAtual > 0 && acao.PrecoAtual <= acao.StopLoss)
                        row.DefaultCellStyle.BackColor = WFColor.FromArgb(117, 57, 57); // Vermelho escuro
                    else if (acao.PrecoAtual >= acao.Alvo)
                        row.DefaultCellStyle.BackColor = WFColor.FromArgb(67, 117, 57); // Verde escuro
                    else
                        row.DefaultCellStyle.BackColor = BACKGROUND;
                else
                    if (acao.PrecoAtual >= acao.StopLoss)
                        row.DefaultCellStyle.BackColor = WFColor.FromArgb(117, 57, 57); // Vermelho escuro
                    else if (acao.PrecoAtual <= acao.Alvo)
                        row.DefaultCellStyle.BackColor = WFColor.FromArgb(67, 117, 57); // Verde escuro
                    else
                        row.DefaultCellStyle.BackColor = BACKGROUND;

                row.DefaultCellStyle.SelectionBackColor = row.DefaultCellStyle.BackColor;
                row.DefaultCellStyle.SelectionForeColor = row.DefaultCellStyle.ForeColor;
            }
        }
        private void atualizaGanhoPerda(BDInfoGrid bDInfo)
        {
            decimal valorAtualTotal = bDInfo.Acoes.Sum(a => a.PrecoAtual * a.Quantidade);
            decimal totalInvestido = bDInfo.Acoes.Sum(a => a.PrecoMedio * a.Quantidade);
            decimal ganhoPerda = valorAtualTotal - totalInvestido;

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
        }
        // Atualizar status labels
        private void AtualizarStatusLabels(BDInfoGrid bdInfo)
        {
            decimal totalInvestido = bdInfo.Acoes.Sum(a => a.PrecoMedio * a.Quantidade);
            decimal valorAtualTotal = bdInfo.Acoes.Sum(a => a.PrecoAtual * a.Quantidade);


            decimal saldo = bdInfo.Saldo;
            if (lblTotalInvestido == null)
            {
                MessageBox.Show("lblTotalInvestido está nulo!");
                return;
            }
            lblTotalInvestido.Text = $"Total Investido: {totalInvestido.ToString("C2", new CultureInfo("pt-BR"))}";
            tInvestidoGeral = totalInvestido;

            lblSaldo.Text = $"Saldo: {saldo.ToString("C2", new CultureInfo("pt-BR"))}";      
            atualizaGanhoPerda(bdInfo);

        }
        // Campos para pesquisa no JSON
        
        // Estrutura do banco de dados para o DataGridView
        private class BDInfoGrid
        {
            public List<AcaoInfo> Acoes { get; set; } = new List<AcaoInfo>();
            public decimal Saldo { get; set; } = 0;
        }
        public static Button CriarBotao(string texto, int largura=150, int altura=30)
        {
            return new Button
            {
                Text = texto,
                Width = largura,
                Height = altura,
                Margin = new Padding(10, 10, 0, 0),
                ForeColor = CORFORE,
                BackColor = WFColor.FromArgb(70, 70, 70),
                FlatStyle = FlatStyle.Flat,
                Font = FONTEPADRAO(t:10),
                FlatAppearance = { BorderSize = 0, MouseDownBackColor = MOUSEDOWNCOLOR, MouseOverBackColor = OVERSELECTION }
                
            };
        }
        // Helper para criar labels
        public static WFLabel CriarLabel(string texto)
        {
            return new WFLabel
            {
                Text = texto,
                AutoSize = true,
                Margin = new Padding(0, 0, 20, 0),
                ForeColor = WFColor.LightGray,
                Font = FONTEPADRAO(t:11)
            };
        }
        // ========================================
        // Atualiza valores das ações via Yahoo Finance
        // ========================================
        private async Task AtualizarValoresAsync()
        {
            if (acoes == null || acoes.Count == 0)
            {
                MessageBox.Show("Nenhum ticker adicionado ainda.");
                return;
            }

            try
            {
                // Adiciona sufixo .SA se necessário
                var symbols = acoes.Select(a =>
                    a.Ticker.EndsWith(".SA") ? a.Ticker : a.Ticker + ".SA"
                ).ToArray();

                // Consulta os dados no Yahoo
                var securities = await Yahoo.Symbols(symbols)
                    .Fields(Field.Symbol, Field.RegularMarketPrice)
                    .QueryAsync();

                int atualizados = 0;

                foreach (var acao in acoes)
                {
                    string tickerBusca = acao.Ticker.EndsWith(".SA") ? acao.Ticker : acao.Ticker + ".SA";

                    if (securities.TryGetValue(tickerBusca, out var data) && data.RegularMarketPrice != null)
                    {
                        decimal precoAtual = (decimal)data.RegularMarketPrice;
                        acao.PrecoAtual = precoAtual;
                        acao.Valorizacao = ((precoAtual - acao.PrecoMedio) / acao.PrecoMedio) * 100;
                        atualizados++;
                    }
                }

                AtualizarGrid(new BDInfoGrid { Acoes = acoes });
                atualizaGanhoPerda(new BDInfoGrid { Acoes = acoes});
                SalvarDados();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao buscar dados: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                tickersFiltrados = tickers
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
        private void SalvarDados()
        {
            try
            {
                string json = JsonSerializer.Serialize(acoes, new JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory(Path.GetDirectoryName(caminhoArquivo)!);
                File.WriteAllText(caminhoArquivo, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar dados: {ex.Message}");
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

            // guarda os dados em campos para o handler acessar
            currentXs = xs;
            currentYs = ys;

            // cria label hover se necessário (como filho do Form para ficar acima do gráfico)
            if (lblValorHover == null)
            {
                lblValorHover = new WFLabel
                {
                    AutoSize = true,
                    BackColor = WFColor.FromArgb(40, 40, 40),
                    ForeColor = WFColor.White,
                    Visible = false
                };
                // adiciona no Form (não dentro do grafico) para garantir que fique acima do render
                this.Controls.Add(lblValorHover);
                lblValorHover.BringToFront();
            }

            // remove handlers duplicados antes de adicionar (segurança)
            grafico.MouseMove -= Grafico_MouseMove;
            grafico.MouseLeave -= Grafico_MouseLeave;

            // registra (faça isso sempre após configurar o gráfico)
            grafico.MouseMove += Grafico_MouseMove;
            grafico.MouseLeave += Grafico_MouseLeave;


        }
        private void Grafico_MouseMove(object? sender, MouseEventArgs e)
        {
            try
            {
                if (currentXs == null || currentYs == null || currentXs.Length == 0)
                {
                    lblValorHover.Visible = false;
                    return;
                }

                // Converte pixel → coordenadas
                ScottPlot.Coordinates coord;
                try
                {
                    coord = grafico.Plot.GetCoordinates(new ScottPlot.Pixel(e.X, e.Y));
                }
                catch
                {
                    lblValorHover.Visible = false;
                    return;
                }

                // Se o mouse estiver fora dos limites do eixo X (ou Y), oculta o label
                var limits = grafico.Plot.Axes.GetLimits();
                if (coord.X < limits.XRange.Min || coord.X > limits.XRange.Max ||
    coord.Y < limits.YRange.Min || coord.Y > limits.YRange.Max)

                {
                    lblValorHover.Visible = false;
                    return;
                }

                double mouseX = coord.X;
                int idx = NearestIndexBinarySearch(currentXs, mouseX);
                if (idx < 0 || idx >= currentYs.Length)
                {
                    lblValorHover.Visible = false;
                    return;
                }

                DateTime data = DateTime.FromOADate(currentXs[idx]);
                double preco = currentYs[idx];

                lblValorHover.Text = $"{data:dd/MM/yyyy}  |  R$ {preco:F2}";
                lblValorHover.Visible = true;
                lblValorHover.BringToFront();

                // Converte posição relativa ao Form
                Point formPos = grafico.PointToScreen(new Point(e.X, e.Y));
                Point clientPos = this.PointToClient(formPos);

                int px = clientPos.X + 12;
                int py = clientPos.Y + 12;

                if (px + lblValorHover.Width > this.ClientSize.Width)
                    px = this.ClientSize.Width - lblValorHover.Width - 8;
                if (py + lblValorHover.Height > this.ClientSize.Height)
                    py = this.ClientSize.Height - lblValorHover.Height - 8;

                lblValorHover.Location = new Point(px, py);
            }
            catch
            {
                lblValorHover.Visible = false;
            }
        }

        private void Grafico_MouseLeave(object? sender, EventArgs e)
        {
            // Oculta o label ao sair completamente do gráfico
            if (lblValorHover != null)
                lblValorHover.Visible = false;
        }

        private int NearestIndexBinarySearch(double[] xs, double x)
        {
            int left = 0, right = xs.Length - 1;
            while (left <= right)
            {
                int mid = (left + right) / 2;
                if (xs[mid] < x) left = mid + 1;
                else right = mid - 1;
            }

            if (left == 0) return 0;
            if (left >= xs.Length) return xs.Length - 1;

            double distLeft = Math.Abs(xs[left - 1] - x);
            double distRight = Math.Abs(xs[left] - x);
            return distLeft < distRight ? left - 1 : left;
        }

        public static void AplicarScrollSuave(NumericUpDown controle)
        {
            controle.MouseWheel += (s, e) =>
            {
                var me = (HandledMouseEventArgs)e;
                me.Handled = true;

                if (e.Delta > 0)
                    controle.Value = Math.Min(controle.Maximum, controle.Value + 1);
                else
                    controle.Value = Math.Max(controle.Minimum, controle.Value - 1);
            };
        }
        private void dataGridView1_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].Selected)
            {
                e.PaintBackground(e.ClipBounds, true);

                // Cria uma sobreposição semi-transparente
                using (Brush brilho = new SolidBrush(WFColor.FromArgb(30, WFColor.White)))
                {
                    e.Graphics.FillRectangle(brilho, e.CellBounds);
                }

                e.PaintContent(e.ClipBounds);
                e.Handled = true;
            }
        }
    }

    public class Cotacao
    {
        public DateTime Data { get; set; }
        public double Close { get; set; }
    }
}
