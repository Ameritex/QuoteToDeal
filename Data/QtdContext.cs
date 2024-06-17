using Microsoft.EntityFrameworkCore;
using Quote_To_Deal.Data.Entities;

namespace Quote_To_Deal.Data
{
    public class QtdContext : DbContext
    {
        public QtdContext(DbContextOptions<QtdContext> contextOptions) : base(contextOptions)
        {
        }

        public virtual DbSet<Company> Companies { get; set; }
        public virtual DbSet<Customer> Customers { get; set; }
        public virtual DbSet<CustomerCompany> CustomerCompany { get; set; }
        public virtual DbSet<Quote> Quotes { get; set; }
        public virtual DbSet<QuoteCustomer> QuoteCustomer { get; set; }
        public virtual DbSet<QuoteSalesPerson> QuoteSalesPerson { get; set; }
        public virtual DbSet<SalesPerson> SalesPersons { get; set; }
        public virtual DbSet<Team> Teams { get; set; }
        public virtual DbSet<UnsubscribeEmail> UnsubscribeEmails { get; set; }
        public virtual DbSet<OnceADayEmail> OnceADayEmails{ get; set; }

    }
}
