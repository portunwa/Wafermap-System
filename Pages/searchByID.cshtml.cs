using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Renci.SshNet;
using WaferMap.Models;

namespace WaferMap.Pages
{
    public class searchByIDModel : PageModel
    {

        public WaferMapItem? WaferMapItem { get; set; }

        public string WaferDate = "";

        public IActionResult OnGet()
        {
            if (!String.IsNullOrEmpty(WaferScribeID))
            {
                //Things gettin' serious

                Console.WriteLine(WaferScribeID);

                SshClient sshclient = new(CfgConstants.FTPserverAddr, CfgConstants.FTPuser, CfgConstants.FTPpwd);
                sshclient.Connect();
                SshCommand sc = sshclient.CreateCommand(" cd production ; cd inboxtest ; ls -l " + WaferScribeID);
                sc.Execute();
                string FilesList = sc.Result;

                // In case of inboxtest folder is empty
                // P1R285-02E0

                if (FilesList == "")
                {
                    sc = sshclient.CreateCommand(" cd spansion ; ls -l " + WaferScribeID);
                    sc.Execute();
                    FilesList = sc.Result;

                }
                Console.WriteLine(FilesList);
                FilesNameArray = FilesList.Split("\n");

#pragma warning disable CS8602 // Dereference of a possibly null reference.
                foreach (string? rawUnixCommandLine in FilesNameArray)
                {
                    if (rawUnixCommandLine != "")
                    {
                        string[] fileNameSegments = rawUnixCommandLine.Split(' ');

                        WaferMapItem = new WaferMapItem();

                        WaferMapItem.waferScribeID = fileNameSegments[fileNameSegments.Length - 1];

                        WaferDate = fileNameSegments[fileNameSegments.Length - 4] + "-" + fileNameSegments[fileNameSegments.Length - 5] + "-" + fileNameSegments[fileNameSegments.Length - 2];
                        Console.WriteLine(WaferDate);
                    }

                }
            }
#pragma warning restore CS8602 // Dereference of a possibly null reference.

            return Page();

        }

        [BindProperty(SupportsGet = true)]
        public string? WaferScribeID { get; set; }

        //private readonly String host = "bkkwmap";
        private String[]? FilesNameArray;
        //private readonly String folderName = "production";




    }
}
