IF DB_ID(N'CustomerManagement') IS NULL
    CREATE DATABASE CustomerManagement;
GO

IF DB_ID(N'WorkshopManagement') IS NULL
    CREATE DATABASE WorkshopManagement;
GO

USE CustomerManagement;
GO

IF OBJECT_ID(N'Customer', N'U') IS NULL
    CREATE TABLE Customer (
        CustomerId nvarchar(50) NOT NULL PRIMARY KEY,
        Name nvarchar(200) NULL,
        Address nvarchar(200) NULL,
        PostalCode nvarchar(20) NULL,
        City nvarchar(100) NULL,
        TelephoneNumber nvarchar(30) NULL,
        EmailAddress nvarchar(200) NULL
    );
GO

IF NOT EXISTS (SELECT 1 FROM Customer WHERE CustomerId = N'CUST-100')
    INSERT INTO Customer (CustomerId, Name, Address, PostalCode, City, TelephoneNumber, EmailAddress)
    VALUES (N'CUST-100', N'John Smith', N'Main St 1', N'10000', N'Metropolis', N'+15551234567', N'john.smith@example.com');
GO

IF NOT EXISTS (SELECT 1 FROM Customer WHERE CustomerId = N'CUST-200')
    INSERT INTO Customer (CustomerId, Name, Address, PostalCode, City, TelephoneNumber, EmailAddress)
    VALUES (N'CUST-200', N'Jane Doe', N'Second St 2', N'20000', N'Gotham', N'+15559876543', N'jane.doe@example.com');
GO

USE WorkshopManagement;
GO

IF OBJECT_ID(N'Vehicle', N'U') IS NULL
    CREATE TABLE Vehicle (
        LicenseNumber nvarchar(50) NOT NULL PRIMARY KEY,
        Brand nvarchar(100) NULL,
        [Type] nvarchar(100) NULL,
        OwnerId nvarchar(50) NULL
    );
GO

IF OBJECT_ID(N'MaintenanceJob', N'U') IS NULL
    CREATE TABLE MaintenanceJob (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        ActualEndTime datetime2 NULL,
        ActualStartTime datetime2 NULL,
        CustomerId nvarchar(50) NULL,
        Description nvarchar(500) NULL,
        EndTime datetime2 NOT NULL,
        Notes nvarchar(500) NULL,
        StartTime datetime2 NOT NULL,
        VehicleLicenseNumber nvarchar(50) NULL,
        WorkshopPlanningDate datetime2 NOT NULL
    );
GO

IF OBJECT_ID(N'WorkshopPlanning', N'U') IS NULL
    CREATE TABLE WorkshopPlanning (
        PlanningDate date NOT NULL PRIMARY KEY
    );
GO

IF NOT EXISTS (SELECT 1 FROM Vehicle WHERE LicenseNumber = N'WA1234A')
    INSERT INTO Vehicle (LicenseNumber, Brand, [Type], OwnerId)
    VALUES (N'WA1234A', N'Skoda', N'Octavia', N'CUST-100');
GO

IF NOT EXISTS (SELECT 1 FROM Vehicle WHERE LicenseNumber = N'KR7788P')
    INSERT INTO Vehicle (LicenseNumber, Brand, [Type], OwnerId)
    VALUES (N'KR7788P', N'Toyota', N'Corolla', N'CUST-200');
GO
