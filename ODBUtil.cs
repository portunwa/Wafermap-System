using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace WaferMap
{
    public class ODBUtil
    {

        private OracleConnection con = new OracleConnection(CfgConstants.OracleDBconnstr);

        // Simple query, returns a reader object.
        public OracleDataReader DoQuery(string sqlCommand) {
            con.Open();
            OracleCommand cmd = new OracleCommand(sqlCommand)
            {
                Connection = con,
                CommandType = CommandType.Text
            };
            OracleDataReader reader = cmd.ExecuteReader();

            cmd.Dispose();


            return reader;
        
        }

        public void CloseConnections() {
            con.Dispose();
        }

}
}
