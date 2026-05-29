using LedgerLens.Data.Abstractions;
using LedgerLens.Data.Models;

namespace LedgerLens.Data.Repositories
{
    public sealed class IndividualRepository
    {
        private readonly IConnectionFactory _connectionFactory;

        public IndividualRepository(IConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public Individual? GetIndividual()
        {
            using var connection = _connectionFactory.CreateOpen();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Individual";

            using var reader = command.ExecuteReader();

            if (!reader.Read())
                return null;

            return new Individual
            {
                IndividualName = reader.GetString(0),
                PANNumber = reader.GetString(1),
                PhotoFile = reader.GetString(2),

                InterestAccountCode = reader.GetInt32(3),
                InterestAccountDesc = reader.GetString(4),

                LTCGAccountCode = reader.GetInt32(5),
                LTCGAccountDesc = reader.GetString(6),

                STCGAccountCode = reader.GetInt32(7),
                STCGAccountDesc = reader.GetString(8),

                LTCLAccountCode = reader.GetInt32(9),
                LTCLAccountDesc = reader.GetString(10),

                STCLAccountCode = reader.GetInt32(11),
                STCLAccountDesc = reader.GetString(12),

                RetainedEarningsId = reader.GetInt32(13)
            };
        }
    }
}