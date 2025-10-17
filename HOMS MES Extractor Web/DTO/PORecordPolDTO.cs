namespace HOMS_MES_Extractor_Web.DTO
{
    public class PoRecordPolDto
    {
        public int Id { get; set; }
        public string PO { get; set; }
        public string ProdLine { get; set; }
        public int Qty { get; set; }
        public int Summary { get; set; }
        public string Type { get; set; }
        public DateTime CreatedDate { get; set; }

        public string CreatedDateStr { get; set; }
    }

}
