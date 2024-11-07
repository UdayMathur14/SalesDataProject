﻿using Microsoft.EntityFrameworkCore;
using SalesDataProject.Models;
using SalesDataProject.Models.AuthenticationModels;
using System;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { 
    
    
    }

    public DbSet<Customer> Customers { get; set; }
    public DbSet<ProspectCustomer> Prospects { get; set; }
    public DbSet<BlockedCustomer> BlockedCustomers { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Country> Countries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // You can configure model properties here if needed
    }
}
