using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Renci.SshNet;
using WaferMap.Models;

namespace WaferMap.Pages
{
    public class PreviewMapModel : PageModel
    {

        public WaferMapContent? WaferMapContent { get; set; }

        public IActionResult OnGet()
        {
            string waferMapScribeID = HttpContext.Request.Query["scribeID"];
            string archiveFlag = HttpContext.Request.Query["archive"];

            if (!String.IsNullOrEmpty(waferMapScribeID))
            {
                SshClient sshclient = new(CfgConstants.FTPserverAddr, CfgConstants.FTPuser, CfgConstants.FTPpwd);
                sshclient.Connect();

                string cmd;
                if (archiveFlag != null)
                {
                    cmd = "cd archive/spansion/spansion/" + archiveFlag + " ; cat " + waferMapScribeID;
                }
                else
                {
                    cmd = " cd spansion ; cat " + waferMapScribeID;
                }

                SshCommand sc = sshclient.CreateCommand(cmd);
                sc.Execute();
                string mapDetailLines = sc.Result;
                //Console.WriteLine(mapDetail);

                string[] mapDetail = mapDetailLines.Split("\n");

                WaferMapContent = new WaferMapContent
                {
                    rawMapData = mapDetail
                };


            }
                return Page();
        }
    }
}
