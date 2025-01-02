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
    public DbSet<User> Users { get; set; }
    public DbSet<Country> Countries { get; set; }
    public DbSet<CommonDomains> CommonDomains { get; set; }
    public DbSet<TitleValidationViewModel> Titles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // You can configure model properties here if needed
        modelBuilder.Entity<TitleValidationViewModel>()
       .Property(e => e.CREATED_ON)
       .HasConversion(
           v => v.ToDateTime(TimeOnly.MinValue), // Convert DateOnly to DateTime for the database
           v => DateOnly.FromDateTime(v)        // Convert DateTime back to DateOnly for the app
       );
    }
}
