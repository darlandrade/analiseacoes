using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AnaliseAcoes;

namespace AnaliseAcoes
{
    public partial class FormLiquidaAcoes : Form
    {
        Panel pnDadosVenda, pnBotoes;
        Button btVender;
        Label lbAtivo, lbAtivoTicker, lbQuantidade, lbTotalInvestido, lbLucro, 
            lblTotalInvestidoValor, lbLucroValor, lbTotalPosVenda, lbTotalPosVendaValor;
        
        NumericUpDown nudQuantidade;

        private string tickers;
        private int quantidade;
        private decimal totalInvestidoGeral = 0, pMedio, pAtual;
        private bool comprado;
        private decimal lucro;
        private List<AcaoInfo> acoes;
        private decimal saldoAtual = 0;


        public FormLiquidaAcoes(string ticker, int quantidade, decimal tInvestidoGeral, 
            decimal pMedio, decimal pAtual, bool comprado, List<AcaoInfo> acoes)
        {
            InitializeComponent();
            this.Size = new Size(400, 300);
            this.tickers = ticker;
            this.quantidade = quantidade;
            this.totalInvestidoGeral = tInvestidoGeral;
            this.pMedio = pMedio;
            this.pAtual = pAtual;
            this.comprado = comprado;
            this.acoes = acoes;

            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Form1.BACKGROUND;

            InicializarComponentes();
            this.acoes = acoes;
        }

        private void InicializarComponentes()
        {
            pnDadosVenda = new Panel()
            {
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(10, 10),
                Size = new Size(360, 200)

            };
            this.Controls.Add(pnDadosVenda);
            pnBotoes = new Panel()
            {
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(10, 210),
                Size = new Size(360, 45)
            };
            this.Controls.Add(pnBotoes);

            // Labels
            lbAtivo = Form1.CriarLabel("Ativo: ");
            lbAtivo.Location = new Point(10, 10);
            pnDadosVenda.Controls.Add(lbAtivo);

            lbQuantidade = Form1.CriarLabel("Quantidade: ");
            lbQuantidade.Location = new Point(10, 50);
            pnDadosVenda.Controls.Add(lbQuantidade);

            lbTotalInvestido = Form1.CriarLabel("T. Investido: ");
            lbTotalInvestido.Location = new Point(10, 90);
            pnDadosVenda.Controls.Add(lbTotalInvestido);

            lbLucro = Form1.CriarLabel("Lucro: ");
            lbLucro.Location = new Point(10, 130);
            pnDadosVenda.Controls.Add(lbLucro);
            // TextBox
            lbAtivoTicker = Form1.CriarLabel(tickers);
            lbAtivoTicker.Location = new Point(120, 10);
            pnDadosVenda.Controls.Add(lbAtivoTicker);
            // NumericUpDown
            nudQuantidade = new NumericUpDown()
            {
                ForeColor = Form1.CORFORE,
                BackColor = Color.FromArgb(60, 60, 60),
                Font = Form1.FONTEPADRAO(),
                BorderStyle = BorderStyle.None,
                Width = 50,
                Minimum = 1,
                Maximum = quantidade,
                Value = quantidade,
                Increment = 1,                
            };

            lbTotalPosVendaValor = Form1.CriarLabel("");
            lbTotalPosVendaValor.Location = new Point(120, 170);
            pnDadosVenda.Controls.Add(lbTotalPosVendaValor);

            nudQuantidade.ValueChanged += (s, e) =>
            {
                // Atualiza os valores quando a quantidade é alterada
                CalcularLucro();
            };

            nudQuantidade.Location = new Point(120, 50);
            Form1.AplicarScrollSuave(nudQuantidade);
            pnDadosVenda.Controls.Add(nudQuantidade);

            // Valores calculados
            lblTotalInvestidoValor = Form1.CriarLabel(totalInvestidoGeral.ToString("C2"));
            lblTotalInvestidoValor.Location = new Point(120, 90);
            pnDadosVenda.Controls.Add(lblTotalInvestidoValor);

            lbLucroValor = Form1.CriarLabel("");
            lbLucroValor.Location = new Point(120, 130);
            pnDadosVenda.Controls.Add(lbLucroValor);

            lbTotalPosVenda = Form1.CriarLabel("T. Pós-Venda: ");
            lbTotalPosVenda.Location = new Point(10, 170);
            pnDadosVenda.Controls.Add(lbTotalPosVenda);

            CalcularLucro();


            // Botões
            btVender = Form1.CriarBotao("Vender", largura: 100);
            btVender.Location = new Point(10, 5);
            btVender.Click += btnVender_Click;
            pnBotoes.Controls.Add(btVender);


        }
        // Evento do botão vender
        private void btnVender_Click(object sender, EventArgs e)
        {
            decimal lucroVenda = CalcularLucro();

            if (lucroVenda > 0)
            {
                CarregarSaldo();
                saldoAtual += lucroVenda;
                SalvarSaldo(saldoAtual);
            }

            ExecutarVendaDoAtivo(tickers, (int)nudQuantidade.Value, pAtual);
            this.Close();
        }
        private void ExecutarVendaDoAtivo(string ticker, int quantidadeVendida, decimal precoVenda)
        {
            // Encontra o ativo na lista
            var ativo = acoes.FirstOrDefault(a => a.Ticker.StartsWith(ticker));
            if (ativo == null) return;

            // Calcula novo estado após venda
            int novaQuantidade = ativo.Quantidade - quantidadeVendida;

            if (novaQuantidade <= 0)
            {
                // Remove o ativo da lista se vendeu tudo
                acoes.Remove(ativo);
            }
            else
            {
                // Atualiza quantidade e total investido
                ativo.Quantidade = novaQuantidade;
                ativo.TotalInvestido = novaQuantidade * ativo.PrecoMedio;
            }

            // Salva no arquivo
            SalvarDadosNoArquivoJson();
            btnConfirmar_Click(this, EventArgs.Empty);
        }

        private void SalvarDadosNoArquivoJson()
        {
            string caminho = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"dados", "acoes.json");
            string json = JsonConvert.SerializeObject(acoes, Formatting.Indented);
            File.WriteAllText(caminho, json);
        }
        public event Action AoFechar;
        private void btnConfirmar_Click(object sender, EventArgs e)
        {
            AoFechar?.Invoke();
            this.Close();
        }
        // Faz o calculo do total investido
        private decimal CalcularLucro()
        {
            int quantidade = (int)nudQuantidade.Value;
            decimal totalParaVenda = quantidade * pAtual;
            decimal custo = quantidade * pMedio;

            lucro = comprado ? totalParaVenda - custo : custo - totalParaVenda;

            lbLucroValor.Text = lucro >= 0
                ? $"R$ {lucro.ToString("F2")}"
                : $"-R$ {Math.Abs(lucro).ToString("F2")}";

            lbTotalPosVendaValor.Text = (totalInvestidoGeral + lucro).ToString("F2");

            return lucro;
        }
        private void CarregarSaldo()
        {
            string caminho = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dados", "saldo.json");
            if (File.Exists(caminho))
            {
                string json = File.ReadAllText(caminho);
                var saldoInfo = JsonConvert.DeserializeObject<SaldoInfo>(json);
                saldoAtual = saldoInfo?.Budget ?? 0;
                MessageBox.Show($"Saldo atual carregado: R$ {saldoAtual.ToString("F2")}");
            }
        }
        private void SalvarSaldo(decimal saldo)
        {
            string pastaDados = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dados");
            Directory.CreateDirectory(pastaDados);

            string caminho = Path.Combine(pastaDados, "saldo.json");
            var saldoInfo = new SaldoInfo { Budget = saldo };
            string json = JsonConvert.SerializeObject(saldoInfo, Formatting.Indented);
            File.WriteAllText(caminho, json);
        }
        public class SaldoInfo {  
            public decimal Budget { get; set; }
        }
        private void FormLiquidaAcoes_Load(object sender, EventArgs e)
        {

        }        
    }
}
