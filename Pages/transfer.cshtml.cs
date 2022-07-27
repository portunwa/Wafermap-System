using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using Renci.SshNet;
using System;
using System.Text;

namespace WaferMap.Pages
{
    public class transferModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public string? WaferLotID { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? selectedSupplier { get; set; }

        public List<string>? WaferListInfo = new List<string>();

        public IActionResult OnGet()
        {
            if (!String.IsNullOrEmpty(WaferLotID))
            {
                WaferListInfo = GetWaferLotInfo(WaferLotID);
            }
            return Page();
        }

        public bool TransferCsv()
        {
            // transferLot+"_"+sSource+"_"+ls_xferDest+"_" +sTime+".csv"
            Console.WriteLine("Destination - " + selectedSupplier + "," + WaferLotID);

            if (!String.IsNullOrEmpty(WaferLotID) && !String.IsNullOrEmpty(selectedSupplier))
            {
                string header = "wafer_lot,operation,date_stamp,time_stamp,source,destination,wafer_type,amd_lot,total_wafer,total_good_die,scribe_id1,scribe_id2,scribe_id3,scribe_id4,scribe_id5,scribe_id6,scribe_id7,scribe_id8,scribe_id9,scribe_id10,scribe_id11,scribe_id12,scribe_id13,scribe_id14,scribe_id15,scribe_id16,scribe_id17,scribe_id18,scribe_id19,scribe_id20,scribe_id21,scribe_id22,scribe_id23,scribe_id24,scribe_id25,scribe_id26,scribe_id27,scribe_id28,scribe_id29,scribe_id30,scribe_id31,scribe_id32,scribe_id33,scribe_id34,scribe_id35,scribe_id36,scribe_id37,scribe_id38,scribe_id39,scribe_id40,scribe_id41,scribe_id42,scribe_id43,scribe_id44,scribe_id45,scribe_id46,scribe_id47,scribe_id48,scribe_id49,scribe_id50 ";
                
                // For file name 
                string sSource = WaferListInfo[4];
                string[] sFormattedTime = WaferListInfo[2].Split(":");
                string sTime = sFormattedTime[0] + sFormattedTime[1] + sFormattedTime[2];
                string filename = WaferLotID + "_" + sSource + "_" + selectedSupplier + "_" + sTime + ".csv";

                // For CSV
                string csv = string.Empty;
                string csvContent = GetWaferLotInfoCsv(WaferLotID, selectedSupplier);

                csv += header;
                csv += "\n";
                csv += csvContent;

                Console.WriteLine("FILENAME : " + filename + "\n");
                Console.WriteLine("CONTENT : " + csv);

                try
                {
                    var client = new SftpClient(CfgConstants.FTPserverAddr, CfgConstants.FTPuser, CfgConstants.FTPpwd);
                    client.Connect();
                    var stream = new MemoryStream();
                    var writer = new StreamWriter(stream);
                    writer.Write(csv);
                    writer.Flush();
                    stream.Position = 0;
                    client.ChangeDirectory("/" + CfgConstants.FTPuser + "/cdalert/");
                    client.UploadFile(stream, "/" + CfgConstants.FTPuser + "/cdalert/" + filename);
                } 
                catch
                {
                    return false;
                }

                //string path = @"C:\Users\Satianrapapo\Documents\" + filename;
                //StreamWriter sw = new StreamWriter(path);
                //sw.Write(csv);
                //sw.Close();
                UpdateWaferLog(WaferLotID, selectedSupplier);

            }
            

            return true;
        }



        public List<string[]> GetSupplierNames()
        {
            ODBUtil oDBUtil = new();
            OracleDataReader reader = oDBUtil.DoQuery(" select * from ( " +
                        " select 'AAA' dest , 'None' detail from dual detail " +
                        " union " +
                        " select location dest , location as detail from user_location where location_role in  ('ASSEMBLY' , 'SUBCON') " +
                        " )order by dest ");

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

        public List<string> GetWaferLotInfo(string waferLotId)
        {
            ODBUtil oDBUtil = new();
            string ls_select = " select distinct  sl.WAFER_LOT as \"Foundry Lot\" , sl.DATE_STAMP as \"Create Date\"  " +
                    " , substr(sl.time_stamp,1,2)||':'||substr(sl.time_stamp,3,2)||':'||substr(sl.time_stamp,5,2) as \"Create Time\"  " +
                    " , SUPPLIER as \"Sort\" , sl.DESTINATION as \"Location\" , PART_NO as \"Device\"   " +
                    " , TOTAL_WAFER as \"Wafer Qty.\"  , TOTAL_GOOD_DIE as \"Good Die Qty.\"   " +
                    " ,status " +
                    " from shipping_list sl  " +
                    " inner join wafer_list wl on sl.wafer_lot = wl.wafer_lot and sl.date_stamp = wl.date_stamp and sl.time_stamp = wl.time_stamp  " +
                    " where sl.wafer_lot = '" + waferLotId + "' and status = 'success X-fer'  ";
            OracleDataReader reader = oDBUtil.DoQuery(ls_select);

            List<string> waferInfo = new List<string>();

            while (reader.Read())
            {
                for (int i = 0; i < 9; i++)
                {
                    waferInfo.Add(reader.GetString(i));
                    //Console.WriteLine(reader.GetString(i));
                }
            }
            reader.Dispose();
            oDBUtil.CloseConnections();
            return waferInfo;

        }

        public string GetWaferLotInfoCsv(string waferLotId, string destination)
        {
            ODBUtil oDBUtil = new();
            string ls_select = " select s.wafer_lot||','||'3000'||','||s.date_stamp||','||S.TIME_STAMP||','||s.destination||','||'" + destination + "'||','||S.WAFER_TYPE||',,'||s.total_wafer||','||S.TOTAL_GOOD_DIE||','||wfid lotinfo   " +
                " from wafer_log l " +
                " inner join  shipping_list s on s.wafer_lot = l.wafer_lot    " +
                " inner join (select wafer_lot , date_Stamp ,  time_stamp  " +
                " , rtrim (xmlagg (xmlelement (e, WAFER_SCRIBE_ID || ',')).extract ('//text()'), ',') wfid  " +
                "  from wafer_list group by wafer_lot , date_stamp , time_stamp ) w on w.wafer_lot = s.wafer_lot and w.date_stamp = s.date_Stamp and s.time_stamp = w.time_stamp   " +
                " where s.wafer_lot = '" + waferLotId + "' and l.status = 'success X-fer' and work_point is null   ";
            OracleDataReader reader = oDBUtil.DoQuery(ls_select);

            string waferInfoCsv = "";
            while (reader.Read())
            {
                //Console.WriteLine(reader.GetString(0));
                waferInfoCsv = reader.GetString(0);
            }
            reader.Dispose();
            oDBUtil.CloseConnections();
            return waferInfoCsv;

        }

        private bool UpdateWaferLog(string waferLotId, string destination)
        {
            var result = false;
            OracleConnection con = new OracleConnection(CfgConstants.OracleDBconnstr);
            string sqlInsertWaferList = " INSERT INTO WAFER_LOG " +
                                        "  select '" + waferLotId + "' lot " +
                                        "  ,to_char(sysdate,'yyyy-mm-dd')dates " +
                                        "  , to_char(sysdate,'HH24mmss')times " +
                                        "  ,'" + destination + "' status " +
                                        "   ,'Post CDAlert file to server' state " +
                                        "  , 'CD-Alert' workpoint " +
                                        "  , null host " +
                                        "  , null folder " +
                                        "  from dual ";
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

    }
}
