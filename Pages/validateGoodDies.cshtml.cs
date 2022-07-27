using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using Renci.SshNet;

namespace WaferMap.Pages
{
    public class validateGoodDiesModel : PageModel
    {

        [BindProperty]
        public string? WaferLotID { get; set; }

        public List<string> WaferInformation = new List<string>();

        public List<string[]> WaferList = new List<string[]>();

        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            Console.WriteLine(WaferLotID);

            ODBUtil oDBUtil = new();
            OracleDataReader reader = oDBUtil.DoQuery("SELECT sl.wafer_lot, sl.part_no, sl.total_good_die, wl.map_filename, wl.good_die_qty, wl.date_stamp, substr(wl.time_stamp,1,2)||':'||substr(wl.time_stamp,3,2)||':'||substr(wl.time_stamp,5,2) AS timestamp FROM shipping_list sl JOIN wafer_list wl ON wl.wafer_lot = sl.wafer_lot WHERE sl.wafer_lot = '" + WaferLotID +"'");

            int totalEntireLotGoodDie = -1;
            string lotPartNo = "";
            string waferlot = "";
            KeyValuePair<string, int> scribeIDDieAmountPair;
            List<KeyValuePair<string, int>> scribeIDAmntPairList = new();

            while (reader.Read())
            {
                if (totalEntireLotGoodDie == -1 || string.IsNullOrEmpty(lotPartNo))
                {
                    waferlot = reader.GetString(0);
                    lotPartNo = reader.GetString(1);
                    totalEntireLotGoodDie = reader.GetInt32(2);
                    WaferInformation.Add(waferlot);
                    WaferInformation.Add(lotPartNo);
                    WaferInformation.Add(totalEntireLotGoodDie.ToString());
                    WaferInformation.Add(reader.GetString(5));
                    WaferInformation.Add(reader.GetString(6));

                }
                scribeIDDieAmountPair = new(reader.GetString(3), reader.GetInt32(4));
                scribeIDAmntPairList.Add(scribeIDDieAmountPair);
            }



            //test area | LDR2850

            foreach (var item in scribeIDAmntPairList)
            {
                Console.WriteLine(item.Key+" "+item.Value);
            }

            int numberOfTrue = 0;
            List<KeyValuePair<string, bool>>? testList = ValidateMapListDieAmnt(totalEntireLotGoodDie, waferlot, lotPartNo, scribeIDAmntPairList);
            foreach (var item in testList)
            {
                Console.WriteLine(item.Key+" "+ item.Value);
                if (item.Value) { numberOfTrue++; }
            }

            float percentMatch = numberOfTrue*100/testList.Count;

            Console.WriteLine("Percent Matched: " + percentMatch);
            WaferInformation.Add(percentMatch.ToString());

            reader.Dispose();
            oDBUtil.CloseConnections();
            return Page();
        }

        public IActionResult OnGetSearch(string term)
        {
            ODBUtil odbutil = new();
            OracleDataReader reader = odbutil.DoQuery("SELECT DISTINCT wafer_lot FROM wafer_log WHERE wafer_lot LIKE '" + term + "%'");
            List<string> likelyNames = new();
            while (reader.Read())
            {
                likelyNames.Add(reader.GetString(0));
            }
            reader.Dispose();
            odbutil.CloseConnections();
            if (likelyNames.Count <= 50)
            { // Limit the candidates so the browser doesn't crash. 
                return new JsonResult(likelyNames);
            }
            else
            {
                return new JsonResult("Too many matches");
            }
        }

        public List<KeyValuePair<string, bool>>? ValidateMapListDieAmnt(int totalLotGoodDies, string waferlot, string partNo, List<KeyValuePair<string, int>> mapDieList) 
        {
            int totalMapGoodDies = 0;
            bool notMatchFlag = false;
            List<KeyValuePair<string, bool>>? validatedGoodDieList = new List<KeyValuePair<string, bool>>();
            SshClient sshclient = new(CfgConstants.FTPserverAddr, CfgConstants.FTPuser, CfgConstants.FTPpwd);
            sshclient.Connect();

            foreach (var item in mapDieList)
            {
                int numGoodDies = countGoodDies(item.Key, sshclient);
                totalMapGoodDies += item.Value;

                if (item.Value == numGoodDies)
                {
                    Console.WriteLine("Number of GoodDies from {0} is {1} = {2} -> EQAUL (TRUE)", item.Key, item.Value, numGoodDies);
                    validatedGoodDieList.Add(new KeyValuePair<string, bool>(item.Key, true));
                }
                else
                {
                    Console.WriteLine("Number of GoodDies from {0} is {1} != {2} -> NOT EQAUL (FALSE)", item.Key, item.Value, numGoodDies);
                    validatedGoodDieList.Add(new KeyValuePair<string, bool>(item.Key, false));
                    notMatchFlag = true;
                    MoveRenameWafer(item.Key, waferlot, sshclient); //can move, but have problem with path username?
                }
                string[] result = { item.Key, item.Value.ToString(), numGoodDies.ToString() };
                WaferList.Add(result);

            }

            if (notMatchFlag)
            {
                UpdateStatus(waferlot, 2);
                UpdateWaferLog(waferlot, 2);
                Console.WriteLine("Update the database");
            }

            if (totalMapGoodDies != totalLotGoodDies)
            {
                Console.WriteLine("Number of totalGoodDies ({0}) is not equal to the sum of number in DB ({1})", totalLotGoodDies, totalMapGoodDies );
            }
            sshclient.Dispose();

            return validatedGoodDieList;
        }

        private int countGoodDies(string waferScribeId, SshClient sshclient)
        {
            int countDies = 0;
            
            SshCommand sc = sshclient.CreateCommand(" cd spansion ; cat " + waferScribeId);
            sc.Execute();
            string mapDetailLines = sc.Result;

            string[] mapDetail = mapDetailLines.Split("\n");

            int count = 0;
            foreach (string content in mapDetail)
            {
                count++;
                if (count > 4)
                {
                    foreach (char c in content)
                    {
                        if (c == '1') { countDies++; }
                    }
                }
            }
            sc.Dispose();
            return countDies;
        }

        private bool UpdateWaferLog(string waferLotId, int code)
        {
            var result = false;
            OracleConnection con = new OracleConnection(CfgConstants.OracleDBconnstr);
            string sqlInsertWaferList = "UPDATE WAFER_LOG " +
                                    " SET (STATUS, STATE ) = (SELECT status, possible_cause  " +
                                    " 						  FROM possible_status  " +
                                    "                         WHERE possible_code = " + code + ")  " +
                                    " WHERE wafer_lot = '" + waferLotId + "' AND status = 'fail X-fer'  ";
            con.Open();
            OracleCommand cmd = new OracleCommand(sqlInsertWaferList)
            {
                Connection = con,
                CommandType = System.Data.CommandType.Text
            };

            int rowsUpdated = cmd.ExecuteNonQuery();

            if (rowsUpdated > 0) { result = true; }

            cmd.Dispose();
            con.Dispose();

            return result;
        }
        private bool UpdateStatus(string waferLotId, int code)
        {
            var result = false;
            OracleConnection con = new OracleConnection(CfgConstants.OracleDBconnstr);
            string sqlInsertWaferList = "UPDATE WAFER_LIST SET STATUS = (SELECT STATUS FROM POSSIBLE_STATUS WHERE POSSIBLE_CODE = " + code + "),POSSIBLE_CODE = " + code + " WHERE WAFER_LOT = '" + waferLotId + "'";
            con.Open();
            OracleCommand cmd = new OracleCommand(sqlInsertWaferList)
            {
                Connection = con,
                CommandType = System.Data.CommandType.Text
            };

            int rowsUpdated = cmd.ExecuteNonQuery();

            if (rowsUpdated > 0) { result = true; }

            cmd.Dispose();
            con.Dispose();

            return result;
        }

        private void MoveRenameWafer(string holdscribe, string waferlot, SshClient sshclient)
        {
            SshCommand sc = sshclient.CreateCommand("mv spansion/" + holdscribe + " spansion/camtekhold/" + holdscribe + "_" + waferlot + "_");
            sc.Execute();
            Console.WriteLine("Move to hold folder and rename: " + holdscribe + " of waferLot: " + waferlot);
            Console.WriteLine(sc.Error);
            sc.Dispose();
        }

        // MOVE: "mv /mcpmap/spansion/"+holdscribe+" /mcpmap/spansion/camtekhold/"+holdscribe+"_"+ls_waferlot+"_"+holdstatus+" ; " ;
        // RENAME: <Wafer Scribe>_<Wafer Lot>_
    }
}
