using LedgerLens.Data.Abstractions;
using Microsoft.Extensions.Options;
using System.Data.OleDb;

namespace LedgerLens.Data
{
    public sealed class OleDbOptions
    {
        public string ConnectionString { get; set; } = "";
    }

    public sealed class OleDbConnectionFactory : IConnectionFactory
    {
        private readonly string _cs;
        public OleDbConnectionFactory(IOptions<OleDbOptions> options)
            => _cs = options.Value.ConnectionString;

        public OleDbConnection CreateOpen()
        {
            var con = new OleDbConnection(_cs);
            con.Open();
            return con;
        }
    }
}
