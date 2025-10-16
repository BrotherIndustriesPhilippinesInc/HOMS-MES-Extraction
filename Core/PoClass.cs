namespace Core
{
    public class PoRecord
    {
        public int Id { get; set; }
        public string PO { get; set; }
        public string ProdLine { get; set; }
        public string Type { get; set; }
        public int Summary { get; set; }

        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }

}
