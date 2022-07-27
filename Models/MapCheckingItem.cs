using System.ComponentModel.DataAnnotations;

namespace WaferMap.Models
{
    public class MapCheckingItem
    {
        public string? WaferLotID { get; set; }

        public bool HasPreAlert { get; set; } = false;

        public bool HasMapFile { get; set; } = false;

        public DateTime PreAlertDate { get; set; }

        public DateTime MapfileDate { get; set; }

        public string? RawMapName { get; set; }

        public string? PreAlertName { get; set; }

        public MapCheckingItem(string waferLotID) { 
            this.WaferLotID = waferLotID;
        }
    }
}
