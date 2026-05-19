using System.Collections.Generic;

namespace SalesDataProject.Models
{
 public class CustomerListViewModel
 {
 public List<Customer> Customers { get; set; } = new List<Customer>();
 public int CurrentPage { get; set; }
 public int PageSize { get; set; }
 public int TotalCount { get; set; }
 public int TotalPages => PageSize >0 ? (int)System.Math.Ceiling((double)TotalCount / PageSize) :0;
 }
}