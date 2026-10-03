IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE TABLE [Guests] (
        [Id] int NOT NULL IDENTITY,
        [FirstName] nvarchar(50) NOT NULL,
        [LastName] nvarchar(50) NOT NULL,
        [Email] nvarchar(150) NULL,
        [Phone] nvarchar(30) NOT NULL,
        [IdType] nvarchar(30) NULL,
        [IdNumber] nvarchar(50) NULL,
        [Nationality] nvarchar(60) NULL,
        [Address] nvarchar(250) NULL,
        [DateOfBirth] datetime2 NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Guests] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(150) NOT NULL,
        [Message] nvarchar(500) NOT NULL,
        [Type] nvarchar(20) NOT NULL,
        [IsRead] bit NOT NULL,
        [Link] nvarchar(250) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE TABLE [RoomTypes] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(50) NOT NULL,
        [Description] nvarchar(250) NULL,
        [Amenities] nvarchar(250) NULL,
        [BasePrice] decimal(18,2) NOT NULL,
        [Capacity] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_RoomTypes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE TABLE [Staff] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(100) NOT NULL,
        [Email] nvarchar(150) NOT NULL,
        [Phone] nvarchar(30) NULL,
        [Username] nvarchar(50) NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [Role] nvarchar(20) NOT NULL,
        [IsActive] bit NOT NULL,
        [HireDate] datetime2 NOT NULL,
        [LastLoginAt] datetime2 NULL,
        [FailedLoginAttempts] int NOT NULL,
        [LockoutEnd] datetime2 NULL,
        [SecurityStamp] nvarchar(64) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Staff] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE TABLE [Rooms] (
        [Id] int NOT NULL IDENTITY,
        [RoomNumber] nvarchar(10) NOT NULL,
        [Floor] int NOT NULL,
        [RoomTypeId] int NOT NULL,
        [PricePerNight] decimal(18,2) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Description] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Rooms] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Rooms_RoomTypes_RoomTypeId] FOREIGN KEY ([RoomTypeId]) REFERENCES [RoomTypes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE TABLE [Bookings] (
        [Id] int NOT NULL IDENTITY,
        [BookingNumber] nvarchar(20) NOT NULL,
        [GuestId] int NOT NULL,
        [RoomId] int NOT NULL,
        [CheckInDate] date NOT NULL,
        [CheckOutDate] date NOT NULL,
        [Adults] int NOT NULL,
        [Children] int NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [PricePerNight] decimal(18,2) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [SpecialRequests] nvarchar(500) NULL,
        [ActualCheckIn] datetime2 NULL,
        [ActualCheckOut] datetime2 NULL,
        [CancelledAt] datetime2 NULL,
        [CancellationReason] nvarchar(250) NULL,
        [CreatedBy] nvarchar(100) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Bookings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Bookings_Guests_GuestId] FOREIGN KEY ([GuestId]) REFERENCES [Guests] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Bookings_Rooms_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [Rooms] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE TABLE [HousekeepingLogs] (
        [Id] int NOT NULL IDENTITY,
        [RoomId] int NOT NULL,
        [FromStatus] nvarchar(20) NOT NULL,
        [ToStatus] nvarchar(20) NOT NULL,
        [Note] nvarchar(250) NULL,
        [ChangedBy] nvarchar(100) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_HousekeepingLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HousekeepingLogs_Rooms_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [Rooms] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE TABLE [Invoices] (
        [Id] int NOT NULL IDENTITY,
        [InvoiceNumber] nvarchar(20) NOT NULL,
        [BookingId] int NOT NULL,
        [IssuedAt] datetime2 NOT NULL,
        [DueDate] datetime2 NULL,
        [Subtotal] decimal(18,2) NOT NULL,
        [ExtraCharges] decimal(18,2) NOT NULL,
        [Discount] decimal(18,2) NOT NULL,
        [TaxRate] decimal(5,4) NOT NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        [AmountPaid] decimal(18,2) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Invoices_Bookings_BookingId] FOREIGN KEY ([BookingId]) REFERENCES [Bookings] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] int NOT NULL IDENTITY,
        [ReceiptNumber] nvarchar(20) NOT NULL,
        [InvoiceId] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Method] nvarchar(20) NOT NULL,
        [PaidAt] datetime2 NOT NULL,
        [Reference] nvarchar(100) NULL,
        [ReceivedBy] nvarchar(100) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Bookings_BookingNumber] ON [Bookings] ([BookingNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Bookings_GuestId] ON [Bookings] ([GuestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Bookings_RoomId_CheckInDate_CheckOutDate] ON [Bookings] ([RoomId], [CheckInDate], [CheckOutDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Bookings_Status] ON [Bookings] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Guests_Email] ON [Guests] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Guests_LastName_FirstName] ON [Guests] ([LastName], [FirstName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Guests_Phone] ON [Guests] ([Phone]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_HousekeepingLogs_RoomId] ON [HousekeepingLogs] ([RoomId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Invoices_BookingId] ON [Invoices] ([BookingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Invoices_InvoiceNumber] ON [Invoices] ([InvoiceNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_Status] ON [Invoices] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_IsRead_CreatedAt] ON [Notifications] ([IsRead], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_InvoiceId] ON [Payments] ([InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_PaidAt] ON [Payments] ([PaidAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Payments_ReceiptNumber] ON [Payments] ([ReceiptNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Rooms_RoomNumber] ON [Rooms] ([RoomNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Rooms_RoomTypeId] ON [Rooms] ([RoomTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Rooms_Status] ON [Rooms] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RoomTypes_Name] ON [RoomTypes] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Staff_Email] ON [Staff] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Staff_Username] ON [Staff] ([Username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928154802_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260928154802_InitialCreate', N'9.0.9');
END;

COMMIT;
GO

