using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using Renci.SshNet;
using WaferMap.Models;

namespace WaferMap.Pages
{
    public class archiveModel : PageModel
    {

        [BindProperty(SupportsGet = true)]
        public string? WaferLotID { get; set; }

        public List<string> WaferIDList = new List<string>();
        public List<string[]> WaferIDListDB = new List<string[]>();

        public string? WaferDate;
        public string? pathLocation;

        public IActionResult OnGet()
        {
            if (!String.IsNullOrEmpty(WaferLotID))
            {
                Console.WriteLine(WaferLotID);

                ODBUtil oDBUtil = new();
                OracleDataReader reader = oDBUtil.DoQuery("SELECT sl.wafer_lot, wl.map_filename, wl.date_stamp FROM shipping_list sl JOIN wafer_list wl ON wl.wafer_lot = sl.wafer_lot WHERE sl.wafer_lot = '" + WaferLotID + "'");

                while (reader.Read())
                {
                    WaferDate = reader.GetString(2);
                    WaferIDList.Add(reader.GetString(1));
                }

                foreach (var item in WaferIDList)
                {
                    Console.WriteLine("WaferIDList = "+item);
                }

                string waferYear = WaferDate.Split("-")[0];
                int waferMonth = Int32.Parse(WaferDate.Split("-")[1]);
                string waferQuarter;

                if (waferMonth >= 1 && waferMonth <= 3)
                    waferQuarter = "1";
                else if (waferMonth >= 4 && waferMonth <= 6)
                    waferQuarter = "2";
                else if (waferMonth >= 7 && waferMonth <= 9)
                    waferQuarter = "3";
                else
                    waferQuarter = "4";

                Console.WriteLine(waferYear + " | " + waferMonth.ToString() + " -> " + waferQuarter);
                //test area | LDR2850, 33T00783002F3 (JJ140U0)

                SshClient sshclient = new(CfgConstants.FTPserverAddr, CfgConstants.FTPuser, CfgConstants.FTPpwd);
                sshclient.Connect();

                foreach (string wafer in WaferIDList)
                {
                    FindDatabaseMap(sshclient, waferQuarter, waferYear, wafer);
                }
                pathLocation = "spansion_" + waferYear + "Q" + waferQuarter;

                reader.Dispose();
                oDBUtil.CloseConnections();
                sshclient.Dispose();
                
            }

            return Page();
        }

        private void FindDatabaseMap(SshClient sshclient, string waferQuarter, string waferYear, string waferID)
        {
            Console.WriteLine(" cd archive/spansion/spansion/spansion_" + waferYear + "Q" + waferQuarter + " ; ls -l " + waferID);
            SshCommand sc = sshclient.CreateCommand(" cd archive/spansion/spansion/spansion_"+waferYear+"Q"+waferQuarter+" ; ls -l "+waferID);
            sc.Execute();
            string MapList = sc.Result;

            if (MapList != "")
            {
                string[] fileNameSegments = MapList.Split(' ');
                string waferName = fileNameSegments[fileNameSegments.Length - 1];
                string waferArchiveDate = fileNameSegments[fileNameSegments.Length - 4] + "-" + fileNameSegments[fileNameSegments.Length - 6] + "-" + fileNameSegments[fileNameSegments.Length - 2];
                string[] result = { waferName, waferArchiveDate };
                WaferIDListDB.Add(result);
            }

            sc.Dispose();
        }
    }
}
