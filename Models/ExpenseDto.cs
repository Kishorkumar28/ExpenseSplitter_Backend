namespace ExpenseSplitterAPI.Models
{
    public class ExpenseDto
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public string PaidByUsername { get; set; }
    }

}
