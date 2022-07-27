using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Oracle.ManagedDataAccess.Client;
using Renci.SshNet;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using WaferMap.Models;


namespace WaferMap.Pages
{
    public class SearchByLotModel : PageModel
    {
        [BindProperty]
        public string? WaferLotID { get; set; }

        public List<string[]> WaferRecord = new List<string[]>();

        public List<string[]> WaferTransaction = new List<string[]>();

        /* public string WaferSummary = ""; */

        public IActionResult OnGet()
        {
            
            return Page();
        }

        public IActionResult OnPost() {
            Console.WriteLine(WaferLotID);

            OracleConnection con = new OracleConnection(CfgConstants.OracleDBconnstr);
            con.Open();
            Console.WriteLine("Connected to Oracle Database {0}", con.ServerVersion);

            

            string cmdQueryForRecord = "SELECT WAFER_LOT , sl.DATE_STAMP AS create_date , substr(time_stamp,1,2)||':'||substr(time_stamp,3,2)||':'||substr(time_stamp,5,2) AS create_time, SUPPLIER,DESTINATION, PART_NO AS Product_name,TOTAL_WAFER , TOTAL_GOOD_DIE FROM shipping_list sl WHERE wafer_lot LIKE '" + WaferLotID + "%'";

            OracleCommand cmdRecord = new OracleCommand(cmdQueryForRecord)
            {
                Connection = con,
                CommandType = CommandType.Text
            };

            // Execute command, create OracleDataReader object
            OracleDataReader readerRecord = cmdRecord.ExecuteReader();


            while (readerRecord.Read())
            {
                Console.WriteLine("RECORD : " + readerRecord.GetString(0) + " , "  + readerRecord.GetString(1) + " , " + readerRecord.GetString(2) +
                    " , " + readerRecord.GetString(3) + " , " + readerRecord.GetString(4) + " , " + readerRecord.GetString(5) + " , " + readerRecord.GetString(6) +
                    " , " + readerRecord.GetString(7));
                string[] result = { readerRecord.GetString(0), readerRecord.GetString(1), readerRecord.GetString(2), readerRecord.GetString(3), 
                    readerRecord.GetString(4), readerRecord.GetString(5), readerRecord.GetString(6), readerRecord.GetString(7) };
                WaferRecord.Add(result);

                

            }

            string cmdQuery = "SELECT wafer_lot , date_stamp as process_date, substr(time_stamp,1,2)||':'||substr(time_stamp,3,2)||':'||substr(time_stamp,5,2) AS process_time  ," +
                " status , state , nvl(work_point , '-') AS remark FROM wafer_log WHERE wafer_lot LIKE '" + WaferLotID + "%' ORDER BY date_stamp , time_stamp ";

            // Create the OracleCommand
            OracleCommand cmd = new OracleCommand(cmdQuery)
            {
                Connection = con,
                CommandType = CommandType.Text
            };

            // Execute command, create OracleDataReader object
            OracleDataReader reader = cmd.ExecuteReader();


            while (reader.Read())
            {
                Console.WriteLine("Wafer Lot ID : " + reader.GetString(0) + " , " +
                  "Proc Date : " + reader.GetString(1) + " , " + reader.GetString(2) + " , " + reader.GetString(3) + " , " + reader.GetString(4) +
                  " , " + reader.GetString(5));
                string[] resultTransaction = { reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5) };
                WaferTransaction.Add(resultTransaction);

            }
            reader.Dispose();
            cmd.Dispose();
            // Clean up

            readerRecord.Dispose();
            cmdRecord.Dispose();

            /*
            ODBUtil odbutil = new ODBUtil();
            string sel_all = " select ( " +
                " select  concat(concat(concat(concat(concat(concat(concat(concat(concat(" +
                " '>>'||wafer_lot||'_none_'||destination||'_'||substr(date_stamp,6,2)||substr(date_stamp,9,2)||substr(date_stamp,3,2)||time_stamp||'.'||supplier||chr(10) " +
                " ,'Date: '||to_char(sysdate , 'mm')||'/'||to_char(sysdate , 'dd')||'/'||to_char(sysdate , 'yyyy' )||chr(9)||to_char(sysdate,'hhmmss')||chr(10) ) " +
                " , 'From: '||supplier||chr(10))    " +
                " , 'To: '||destination||chr(10)   )  " +
                " ,'Product_No/Part_No: '||part_no||chr(10) ) " +
                " ,'Product_description: '||product_desc||chr(10) ) " +
                " , 'Wafer_lot_number: '||wafer_lot||chr(10) ) " +
                " , 'Assembly_lot_number: none'||chr(10)  )  " +
                " , 'Total_wafers: '||total_wafer||chr(10) ) " +
                " , 'Wafer#'||chr(9)||'Wafer_scribe_ID'||chr(9)||'Good_Dies'||chr(9)||'Map_Filename'||chr(10) ) header1 " +
                " from (select * from ( select * from  shipping_list sl where wafer_lot = '" + WaferLotID + "' order by date_stamp desc)where rownum <=1) )|| " +
                " (select replace( wm_concat ( adesc) , ',', '' ) AS sentence from ( " +
                " select wl.wafer_lot , concat(concat(concat( " +
                " chr(32)||wl.wafer_number||chr(9)   , " +
                "  wl.wafer_scribe_id||chr(9) ) ,    " +
                "  wl.good_die_qty||chr(9) ), " +
                //"  wl.original_filename||chr(10) ) adesc  "+ 
                " decode(wr.dest_map_format,'INTERMEDIATE', regexp_replace(wl.original_filename,'xml|XML','zip') ,wl.original_filename)||chr(10) ) adesc " +
                "  from wafer_list wl " +
                //"  inner join ( select* from (select sl.* from shipping_list sl where  wafer_lot = '"+ls_wafer_lot+"' order by date_stamp||time_stamp desc)where rownum <=1) sl  "+
                " inner join ( select* from (select sl.* from shipping_list sl where  wafer_lot = '" + WaferLotID + "' order by date_stamp||time_stamp desc)where rownum <=1) sl" +
                "  on wl.wafer_lot = sl.wafer_lot   and wl.date_stamp||wl.time_stamp = sl.date_stamp||sl.time_stamp " +
                " inner join wafer_route wr on WR.SUPPLIER =SL.SUPPLIER    AND  SL.DESTINATION= WR.DESTINATION " +
                "  order by wafer_number)  " +
                " ) summaryfile from dual ";
            OracleDataReader readerSummary = odbutil.DoQuery(sel_all);
            while (readerSummary.Read())
            {
                WaferSummary = readerSummary.GetString(0);
            }
            readerSummary.Dispose();
            odbutil.CloseConnections();
            */

            con.Dispose();

            return Page();
        }

        public IActionResult OnGetSearch(string term)
        {
            ODBUtil odbutil= new ODBUtil();
            OracleDataReader reader = odbutil.DoQuery("SELECT DISTINCT wafer_lot FROM wafer_log WHERE wafer_lot LIKE '"+term+"%'");
            List<string> likelyNames = new List<string>();
            while (reader.Read())
            {
                likelyNames.Add(reader.GetString(0));
            }
            reader.Dispose();
            odbutil.CloseConnections();
            if(likelyNames.Count <= 50) { // Limit the candidates so the browser doesn't crash. 
                return new JsonResult(likelyNames);
            }
            else
            {
                return new JsonResult("Too many matches");
            }
        }

    }
}
