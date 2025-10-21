using System;
using System.Data.SQLite;
using System.IO;

namespace AnaliseAcoes
{
    public partial class BancoSQLite
    {
        private string caminhoDb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dados", "cotacoes.db");

        public SQLiteConnection Conectar()
        {
            if (!File.Exists(caminhoDb))
                SQLiteConnection.CreateFile(caminhoDb);

            var conn = new SQLiteConnection($"Data Source={caminhoDb};Version=3;");
            conn.Open();
            return conn;
        }

        public void CriarTabela()
        {
            using var conn = Conectar();
            string sql = @"
                CREATE TABLE IF NOT EXISTS cotacoes (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ticker TEXT,
                    data DATE,
                    preco_fechamento REAL
                );";

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }

        public void InserirCotacao(string ticker, DateTime data, double preco)
        {
            using var conn = Conectar();
            string sql = "INSERT INTO cotacoes (ticker, data, preco_fechamento) VALUES (@ticker, @data, @preco);";

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ticker", ticker);
            cmd.Parameters.AddWithValue("@data", data.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@preco", preco);
            cmd.ExecuteNonQuery();
        }
    }
}
