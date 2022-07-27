using System.ComponentModel.DataAnnotations;

namespace WaferMap.Models

{
    public class WaferMapItem
    {
        public string? waferScribeID { get; set; }

        [DataType(DataType.Date)]
        public DateTime waferDate { get; set; }

        public int waferGoodDieAmount { get; set; }



    }
}
