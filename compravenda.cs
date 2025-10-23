using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using static AnaliseAcoes.Form1;

namespace AnaliseAcoes
{
    public partial class FormCompraVenda : Form
    {
        

        private ComboBox cmbAtivo;
        private TextBox txtQuantidade;
        private TextBox txtPreco;
        private TextBox txtStopLoss;
        private TextBox txtAlvo;
        private Button btnSalvar;
        private List<string> tickers;
        private string ativoSelecionado;
        private decimal saldoAtual;
        private TipoOperacao tipoOperacao;

        public FormCompraVenda(TipoOperacao operacao, List<string> tickers, string ativoSelecionado)
        {
            InitializeComponent();
            this.Text = operacao == TipoOperacao.Compra ? "Registrar Compra" : "Registrar Venda";
            tipoOperacao = operacao;
            this.Size = new Size(305, 290);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.tickers = tickers;
            this.ativoSelecionado = ativoSelecionado;
            IniciaFormulario(this, EventArgs.Empty);
        }

        // Evento de carregamento do formulário
        private void IniciaFormulario(object sender, EventArgs e)
        {
            CarregarSaldo();
            // Painel para as labels e campos de entrada
            Panel pnLabels = new Panel
            {
                Location = new Point(10, 10),
                Size = new Size(110, 170),
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(pnLabels);

            // Configurações iniciais do formulário podem ser feitas aqui - LABELS
            string[] lblNomes = { "Ativo", "Quantidade", "Preço Unitário", "StopLoss", "Alvo" };
            for (int i = 0; i < lblNomes.Length; i++)
            {
                Label lbl = CriarLabel(lblNomes[i] + ":", 20 + i * 30);
                pnLabels.Controls.Add(lbl);
            }

            Panel pnEntradas = new Panel
            {
                Location = new Point(130, 10),
                Size = new Size(150, 170),
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(pnEntradas);

            // Campos de entrada correspondentes às labels
            for (int i = 0; i < lblNomes.Length; i++)
            {
                switch(i){
                    case 0:
                        cmbAtivo = new ComboBox()
                        {
                            Location = new Point(12, 10 + i * 30),
                            Size = new Size(120, 20),
                            DropDownStyle = ComboBoxStyle.DropDown,
                            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                            AutoCompleteSource = AutoCompleteSource.ListItems
                        };
                        cmbAtivo.Items.AddRange(tickers.ToArray());
                        cmbAtivo.Text = ativoSelecionado;
                        pnEntradas.Controls.Add(cmbAtivo);
                        break;
                    case 1:
                        txtQuantidade = new TextBox()
                        {
                            Location = new Point(12, 10 + i * 30),
                            Size = new Size(120, 20)
                        };
                        pnEntradas.Controls.Add(txtQuantidade);
                        break;
                    case 2:
                        txtPreco = new TextBox()
                        {
                            Location = new Point(12, 10 + i * 30),
                            Size = new Size(120, 20)
                        };
                        pnEntradas.Controls.Add(txtPreco);
                        break;
                    case 3:
                        txtStopLoss = new TextBox()
                        {
                            Location = new Point(12, 10 + i * 30),
                            Size = new Size(120, 20)
                        };
                        pnEntradas.Controls.Add(txtStopLoss);
                        break;
                    case 4:
                        txtAlvo = new TextBox()
                        {
                            Location = new Point(12, 10 + i * 30),
                            Size = new Size(120, 20)
                        };
                        pnEntradas.Controls.Add(txtAlvo);
                        break;
                }   

                Panel pnBotoes = new Panel
                {
                    Location = new Point(10, 190),
                    Size = new Size(270, 50),
                    BorderStyle = BorderStyle.FixedSingle
                };
                this.Controls.Add(pnBotoes);

                btnSalvar = new Button()
                {
                    Text = "Salvar",
                    Location = new Point(10, 10),
                    Size = new Size(80, 30)
                };
                btnSalvar.Click += (s, e) => { BtnSalvar_Click(); this.DialogResult = DialogResult.OK;
                    this.Close();
                };
                pnBotoes.Controls.Add(btnSalvar);
            }
        }
        // Salva o ativo no json
        private void BtnSalvar_Click()
        {
            string caminho = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dados", "acoes.json");

            int quantidadeNova = int.Parse(txtQuantidade.Text);
            decimal precoNovo = decimal.Parse(txtPreco.Text);
            string ticker = cmbAtivo.SelectedItem?.ToString() ?? "";
            decimal valorizacao = 0;

            List<AcaoInfo> acoes = new();
            if (File.Exists(caminho))
            {
                string jsonExistente = File.ReadAllText(caminho);
                acoes = JsonSerializer.Deserialize<List<AcaoInfo>>(jsonExistente) ?? new();
            }

            var acaoExistente = acoes.FirstOrDefault(a => a.Ticker.Trim().ToUpper() == cmbAtivo.Text.Trim().ToUpper());
            decimal custoComprar = quantidadeNova * precoNovo;
            if (custoComprar > saldoAtual)
            {
                MessageBox.Show("Saldo insuficiente para realizar a compra.");
                return;                
            }
            
            string opera = tipoOperacao == TipoOperacao.Compra ? "C" : "V";

            if (acaoExistente != null)
            {
                
                // Atualiza preço médio e quantidade
                decimal totalInvestidoAnterior = acaoExistente.Quantidade * acaoExistente.PrecoMedio;
                decimal totalInvestidoNovo = quantidadeNova * precoNovo;
                int novaQuantidade = acaoExistente.Quantidade + quantidadeNova;

                acaoExistente.Ticker = cmbAtivo.Text.ToUpper();
                acaoExistente.PrecoMedio = Math.Round((totalInvestidoAnterior + totalInvestidoNovo) / novaQuantidade,4);
                acaoExistente.Quantidade = novaQuantidade;
                acaoExistente.PrecoAtual = precoNovo;
                acaoExistente.StopLoss = decimal.TryParse(txtStopLoss.Text, out decimal sl) ? sl : 0;
                acaoExistente.Alvo = decimal.TryParse(txtAlvo.Text, out decimal alvo) ? alvo : 0;
                acaoExistente.TotalInvestido = novaQuantidade * acaoExistente.PrecoMedio;
                acaoExistente.Data = DateTime.Now;
                acaoExistente.Operacao = opera;
                if (acaoExistente.PrecoMedio > 0)
                {
                    bool estaComprado = tipoOperacao == TipoOperacao.Compra;

                    valorizacao = estaComprado
                        ? (precoNovo - acaoExistente.PrecoMedio) / acaoExistente.PrecoMedio
                        : (acaoExistente.PrecoMedio - precoNovo) / acaoExistente.PrecoMedio;

                    acaoExistente.Valorizacao = Math.Round(valorizacao,4);
                }

            }
            else
            {
                // Cria nova ação
                AcaoInfo novaAcao = new()
                {
                    Ticker = cmbAtivo.Text,
                    Quantidade = quantidadeNova,
                    PrecoMedio = precoNovo,
                    PrecoAtual = precoNovo,
                    Valorizacao = valorizacao,
                    TotalInvestido = quantidadeNova * precoNovo,
                    StopLoss = decimal.TryParse(txtStopLoss.Text, out decimal sl) ? sl : 0,
                    Alvo = decimal.TryParse(txtAlvo.Text, out decimal alvo) ? alvo : 0,
                    Data = DateTime.Now,
                    Operacao = opera
                };

                acoes.Add(novaAcao);
            }

            AtualizarSaldo(-custoComprar);
            // Salva no arquivo
            string novoJson = JsonSerializer.Serialize(acoes, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(caminho, novoJson);

        }
        private Label CriarLabel(string texto, int posY)
        {
            return new Label() 
            { 
                Text = texto,
                Location = new Point(10, posY),
                AutoSize = true
            };           

        }
        private void AtualizarSaldo(decimal valor)
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

                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erro ao ler saldo: " + ex.Message);
                }
            }
        }
        // carrega o saldo que temos
        private void CarregarSaldo()
        {
            // Lê o arquivo saldo.json
            string caminhoSaldo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dados", "saldo.json");
            if (File.Exists(caminhoSaldo))
            {
                try
                {
                    string json = File.ReadAllText(caminhoSaldo);
                    SaldoInfo info = JsonSerializer.Deserialize<SaldoInfo>(json);
                    saldoAtual = info?.Budget ?? 0;
            

                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erro ao ler saldo: " + ex.Message);
                }
            }

        }
        private class AcaoInfo
        {
            public string Ticker { get; set; }
            public int Quantidade { get; set; }
            public decimal PrecoMedio { get; set; }
            public decimal PrecoAtual { get; set; }
            public decimal TotalInvestido { get; set; }
            public decimal StopLoss { get; set; }
            public decimal Alvo { get; set; }
            public decimal Valorizacao { get; set; }
            public DateTime Data { get; set; }
            public string Operacao { get; set; }
        }       
        private void FormCompraVenda_Load(object sender, EventArgs e)
        {

        }
    }
}
