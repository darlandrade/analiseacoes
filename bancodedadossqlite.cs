using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.IO;

namespace AnaliseAcoes
{
    public class BancoSQLite
    {
        private string caminhoDb;

        public BancoSQLite()
        {
            string pastaDados = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dados");
            Directory.CreateDirectory(pastaDados); // garante que a pasta exista
            caminhoDb = Path.Combine(pastaDados, "cotacoes.db");
        }

        public void CriarTabela()
        {
            using var conn = new SqliteConnection($"Data Source={caminhoDb}");
            conn.Open();

            string sql = @"
                CREATE TABLE IF NOT EXISTS cotacoes (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ticker TEXT,
                    data DATE,
                    preco_fechamento REAL
                );";

            using var cmd = new SqliteCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }
        public string CaminhoDb => caminhoDb;

        public void RecriarTabela()
        {
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={caminhoDb}");
            conn.Open();
            string drop = "DROP TABLE IF EXISTS cotacoes;";
            string create = @"
        CREATE TABLE cotacoes (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            ticker TEXT,
            data DATE,
            preco_fechamento REAL
        );";
            using (var cmd = new Microsoft.Data.Sqlite.SqliteCommand(drop, conn)) cmd.ExecuteNonQuery();
            using (var cmd = new Microsoft.Data.Sqlite.SqliteCommand(create, conn)) cmd.ExecuteNonQuery();
        }

        public void InserirCotacao(string ticker, DateTime data, double preco)
        {
            using var conn = new SqliteConnection($"Data Source={caminhoDb}");
            conn.Open();

            string sql = @"
                INSERT INTO cotacoes (ticker, data, preco_fechamento)
                VALUES (@ticker, @data, @preco);";

            using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ticker", ticker);
            cmd.Parameters.AddWithValue("@data", data.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@preco", preco);
            cmd.ExecuteNonQuery();
        }

        public List<string> ObterTickers()
        {
            var lista = new List<string>();
            using var conn = new SqliteConnection($"Data Source={caminhoDb}");
            conn.Open();

            string sql = "SELECT DISTINCT ticker FROM cotacoes ORDER BY ticker;";
            using var cmd = new SqliteCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
                lista.Add(reader["ticker"].ToString());

            return lista;
        }

        public List<Cotacao> ObterCotacoesSQLite(string ticker, int dias = 21)
        {
            var lista = new List<Cotacao>();
            using var conn = new SqliteConnection($"Data Source={caminhoDb}");
            conn.Open();

            string ultimaDataSql = "SELECT MAX(data) FROM cotacoes WHERE ticker = @ticker;";
            DateTime referencia;

            using (var cmd = new SqliteCommand(ultimaDataSql, conn))
            {
                cmd.Parameters.AddWithValue("@ticker", ticker);
                var result = cmd.ExecuteScalar()?.ToString();
                if (!DateTime.TryParse(result, out referencia))
                    return lista;
            }

            DateTime limite = referencia.AddDays(-dias);

            string sql = @"
                SELECT data, preco_fechamento
                FROM cotacoes
                WHERE ticker = @ticker
                  AND data >= @limite
                  AND data <= @referencia
                ORDER BY data;";

            using var cmdDados = new SqliteCommand(sql, conn);
            cmdDados.Parameters.AddWithValue("@ticker", ticker);
            cmdDados.Parameters.AddWithValue("@limite", limite.ToString("yyyy-MM-dd"));
            cmdDados.Parameters.AddWithValue("@referencia", referencia.ToString("yyyy-MM-dd"));

            using var reader = cmdDados.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new Cotacao
                {
                    Data = DateTime.Parse(reader["data"].ToString()),
                    Close = Convert.ToDouble(reader["preco_fechamento"], CultureInfo.InvariantCulture)
                });
            }

            return lista;
        }
    }

}
