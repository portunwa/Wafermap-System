using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using Renci.SshNet;
using Renci.SshNet.Common;
using System.Net.Sockets;
using WaferMap.Models;

namespace WaferMap.Pages
{
    public class validateMapfilesModel : PageModel
    {
        [BindProperty(Name = "SupCode", SupportsGet = true)]

        public string? SupplierCode { get; set; }
        public string? ExceptionMessage { get; set; }

        public List<MapCheckingItem>? ValidationAssetList = new List<MapCheckingItem>();

        public List<string> ConnectionServerInfo = new List<string>();

        public IActionResult OnGet()
        {
            Console.WriteLine(SupplierCode);
            if (!String.IsNullOrEmpty(SupplierCode)) {
                List<string> connDetail = GetSupplierServerInfo(SupplierCode);
                ConnectionServerInfo = connDetail;
                foreach (var item in connDetail)
                {
                    Console.Write(item+", ");
                }

                string location = connDetail[0];
                string pathName = connDetail[1];
                string userName = connDetail[2];
                string passWd = connDetail[3];

                Console.WriteLine();
                Console.WriteLine("Is SFTP?: "+IsSFTP(location, userName, passWd));
                ConnectionServerInfo.Add(IsSFTP(location, userName, passWd).ToString());

                if (!IsSFTP(location, userName, passWd)) 
                {
                    SshClient? ftpClient = ConnectViaFTP(location, userName, passWd);
                    if (ftpClient != null)
                    {
                    ValidationAssetList = MatchFiles(ftpClient, pathName);
                    }
                }
                else
                {
                    SftpClient? sftpClient = ConnectViaSFTP(location, userName, passWd);
                    if (sftpClient != null)
                    {
                    ValidationAssetList = MatchFiles(sftpClient, pathName);
                    }
                }

            }
                return Page();
        }

        public List<string> GetSupplierServerInfo(string supplierCode) {
            ODBUtil oDBUtil = new();
            OracleDataReader reader = oDBUtil.DoQuery("SELECT host, nvl(path_file,' ') , user_login , passwd  FROM " +
                                                     " (SELECT * FROM ftp_info f " +
                                                     "  WHERE location NOT IN ( SELECT location FROM sftp_info s)  " +
                                                     "  UNION  " +
                                                     "  SELECT * FROM sftp_info  " +
                                                     "  )  " +
                                                     "  WHERE location =  '" + supplierCode + "' ");

            List<string> supplierServerInfo = new List<string>();

            while (reader.Read())
            {
                supplierServerInfo.Add(reader.GetString(0)); //host
                supplierServerInfo.Add(reader.GetString(1)); //path, could be null.
                supplierServerInfo.Add(reader.GetString(2)); //username
                supplierServerInfo.Add(reader.GetString(3)); //password
            }
            reader.Dispose();
            oDBUtil.CloseConnections();
            return supplierServerInfo;
        }

        public List<string[]> GetSupplierNames() {
            ODBUtil oDBUtil = new();
            OracleDataReader reader = oDBUtil.DoQuery("SELECT location , description   FROM active_host a, user_location b " +
                        "WHERE a.schedule_name = 'Checkrawmap' AND a.enable_flag ='Y' " +
                        "AND a.host_name = b.location   ORDER BY b.description");

            List<string[]> AllSuppliers = new List<string[]>();

            while (reader.Read())
            {
                string[] SupplierRowResult = { reader.GetString(0), reader.GetString(1) };
                AllSuppliers.Add(SupplierRowResult);
            }
            reader.Dispose();
            oDBUtil.CloseConnections();
            return AllSuppliers;

        }

        public bool IsSFTP(string location, string userName, string passWd) {

            ODBUtil odbUtil = new();
            string query = "SELECT location FROM sftp_info " +
                    " WHERE host = '" + location + "'  " +
                    " AND user_login = '" + userName + "' " +
                    " AND passwd = '" + passWd + "' ";
            OracleDataReader testSFTPReader = odbUtil.DoQuery(query);

            string result = "";

            while (testSFTPReader.Read())
            {
                result = testSFTPReader.GetString(0);
            }
            testSFTPReader.Dispose();
            odbUtil.CloseConnections();
            if (String.IsNullOrEmpty(result)) {
                return false;
            }
            return true;
        }

        public SshClient? ConnectViaFTP(string location, string userName, string passWd) {
            SshClient sshclient = new(location, userName, passWd);
            try
            {
                sshclient.Connect();
                return sshclient;

            }
            catch (SshAuthenticationException SAE)
            {
                ExceptionMessage = SAE.Message; 
                Console.WriteLine("Conn Detail Incorrect");
                Console.WriteLine("Error: "+SAE.Message);
                Console.WriteLine(SAE.StackTrace);

            }
            catch (SocketException SoE)
            {
                ExceptionMessage = SoE.Message;
                Console.WriteLine("Host Problem");
                Console.WriteLine("Error: "+SoE.Message);
                Console.WriteLine(SoE.StackTrace);
            }
            return null;
        }
        public SftpClient? ConnectViaSFTP(string location, string userName, string passWd) {
            SftpClient sftpClient = new(location, userName, passWd);
            try
            {
                sftpClient.Connect();
                return sftpClient;

            }
            catch (SshAuthenticationException SAE)
            {
                ExceptionMessage = SAE.Message;
                Console.WriteLine("Conn Detail Incorrect");
                Console.WriteLine("Error: " + SAE.Message);
                Console.WriteLine(SAE.StackTrace);

            }
            catch (SocketException SoE)
            {
                ExceptionMessage = SoE.Message;
                Console.WriteLine("Host Problem");
                Console.WriteLine("Error: " + SoE.Message);
                Console.WriteLine(SoE.StackTrace);
            }
            catch (InvalidOperationException IoE)
            {
                ExceptionMessage = IoE.Message;
                Console.WriteLine("Error: " + IoE.Message);
            }
            catch (SshException SE) {
                ExceptionMessage = SE.Message;
                Console.WriteLine(SE.Message);
            }
            return null;

        }

        private List<MapCheckingItem> MatchFiles(SshClient client, string? pathName) {
            SshCommand sc = client.CreateCommand("cd "+pathName+ "notice; find . -name \\*.F25 -type f -printf 'Date: % TY-% Tm-% Td - Time: % TT - File: % f\n';");
            sc.Execute();
            string preAlertConsoleResult = sc.Result;
            string[] preAlertConsoleLineResult = preAlertConsoleResult.Split("\n");
            
            sc = client.CreateCommand("cd " + pathName + "rawmap; find . -name \\*.XML -type f -printf 'Date: % TY-% Tm-% Td - Time: % TT - File: % f\n';");
            sc.Execute();
            string rawMapConsoleResult = sc.Result;
            string[] rawMapConsoleLineResult = rawMapConsoleResult.Split("\n");

            List<string[]> formattedAllValidationItemList = new List<string[]>();
            List<string[]> formattedPreAlertValidationItemList = new List<string[]>();
            List<string[]> formattedRawMapValidationItemList = new List<string[]>();

            foreach (var item in preAlertConsoleLineResult)
            {
                if (!item.Equals(""))
                {
                formattedAllValidationItemList.Add(ExtractFileName(item));
                formattedPreAlertValidationItemList.Add(ExtractFileName(item));

                }
            }
            foreach (var item in rawMapConsoleLineResult)
            {
                if (!item.Equals(""))
                {
                formattedAllValidationItemList.Add(ExtractFileName(item));
                formattedRawMapValidationItemList.Add(ExtractFileName(item));

                }
            }
            sc.Dispose();
            return generateListOfCheckedItems(formattedAllValidationItemList, formattedPreAlertValidationItemList, formattedRawMapValidationItemList);
        }

        private List<MapCheckingItem> MatchFiles(SftpClient client, string? pathname) {
            string concatedRawmapPathname = pathname + "rawmap/";
            try
            {
                client.ChangeDirectory(concatedRawmapPathname);
            }
            catch (SshException SE)
            {
                ExceptionMessage = SE.Message;
                Console.WriteLine(SE.Message);
            }
            var rawMapFiles = client.ListDirectory(client.WorkingDirectory);

            List<string[]> formattedAllValidationItemList = new List<string[]>();
            List<string[]> formattedPreAlertValidationItemList = new List<string[]>();
            List<string[]> formattedRawMapValidationItemList = new List<string[]>();

            foreach (var file in rawMapFiles) {
                if (file.Name != "." && file.Name != "..")
                {
                    string lastWriteDate = file.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss");
                    formattedAllValidationItemList.Add(new string[] { lastWriteDate, file.Name});
                    formattedRawMapValidationItemList.Add(new string[] {lastWriteDate, file.Name});
                }
            }

            string concatedPreAlertPathname = pathname + "notice/";
            try
            {
                client.ChangeDirectory(concatedPreAlertPathname);
            }
            catch (SshException SE)
            {
                ExceptionMessage = SE.Message;
                Console.WriteLine(SE.Message);
            }
            var rawPreAlertFiles = client.ListDirectory(client.WorkingDirectory);

            foreach (var file in rawPreAlertFiles)
            {
                if (file.Name != "." && file.Name != "..")
                {
                    string lastWriteDate = file.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss");
                    formattedAllValidationItemList.Add(new string[] { lastWriteDate, file.Name });
                    formattedPreAlertValidationItemList.Add(new string[] { lastWriteDate, file.Name });
                }
            }

            client.Disconnect();
            client.Dispose();
            return generateListOfCheckedItems(formattedAllValidationItemList, formattedPreAlertValidationItemList, formattedRawMapValidationItemList);
        }

        // Format:
        // yyyy-MM-dd HH:mm:ss Filename
        private string[] ExtractFileName(string rawFileName) {
            //Console.WriteLine("RAW: " + rawFileName);
            string dateStr = rawFileName.Substring(6, 10);
            string timeStr = rawFileName.Substring(25, 8);
            string fileNameStr = rawFileName.Substring(53);
           // Console.WriteLine("Date: "+dateStr+" Time: "+timeStr+" FileName: "+fileNameStr);
            string[] sortedAttributes = { dateStr + " " + timeStr, fileNameStr };
            return sortedAttributes;
        }

        private List<MapCheckingItem> generateListOfCheckedItems(List<string[]> formattedItemList, List<string[]> formattedPreAlertItemList, List<string[]>formattedRawMapItemList) { 

            List<string> listOfLots = new List<string>();
            List<MapCheckingItem> mapCheckingItems = new List<MapCheckingItem> ();

            foreach (var itemTuple in formattedItemList) { // Every item

                string lotNumber;

                // Split Lot Number from filename
                if (itemTuple[1].EndsWith(".XML")) // Map File
                {
                    lotNumber = itemTuple[1].Split("-")[0];
                    listOfLots.Add(lotNumber);

                }
                else
                {
                    lotNumber = itemTuple[1].Split("_")[0]; //Prealert File
                    if (!listOfLots.Contains(lotNumber))
                    {
                        listOfLots.Add(lotNumber);
                    }
                }
                // Now listOfLots contains every non duplicate lot number.
            }

            foreach (var lot in listOfLots)
            {
                // Processing each unique lot into a structured object
                
                MapCheckingItem mapCheckingItem = new(lot);

                foreach (var item in formattedPreAlertItemList)
                {
                    if (item[1].Contains(lot)) // If the list of prealert files has the lot number
                    {
                        mapCheckingItem.HasPreAlert = true;
                        mapCheckingItem.PreAlertDate = parseDateTimeFromFileName(item[0]);
                        mapCheckingItem.PreAlertName = item[1];
                    }
                }

                foreach (var item in formattedRawMapItemList)
                {
                    if (item[1].Contains(lot)) // If the list of map files has the lot number
                    {
                        mapCheckingItem.HasMapFile = true;
                        mapCheckingItem.MapfileDate = parseDateTimeFromFileName(item[0]);
                        mapCheckingItem.RawMapName = item[1];
                    }
                }

                mapCheckingItems.Add(mapCheckingItem);
            }
            return mapCheckingItems;

        }

        private DateTime parseDateTimeFromFileName(string filename) {

            string tempDate = filename.Split(" ")[0];
            string tempTime = filename.Split(" ")[1];

            return new DateTime(
                    Int32.Parse(tempDate.Split("-")[0]),
                    Int32.Parse(tempDate.Split("-")[1]),
                    Int32.Parse(tempDate.Split("-")[2]),
                    Int32.Parse(tempTime.Split(":")[0]),
                    Int32.Parse(tempTime.Split(":")[1]),
                    Int32.Parse(tempTime.Split(":")[2])
                );
        }
        

    }
}
