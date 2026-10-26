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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [AnalysisTypes] (
        [Id] int NOT NULL IDENTITY,
        [TypeName] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [DefaultCost] decimal(18,2) NULL,
        [PreparationInstructions] nvarchar(1000) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AnalysisTypes] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] int NOT NULL IDENTITY,
        [TableName] nvarchar(max) NOT NULL,
        [RecordId] int NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [PerformedBy] int NOT NULL,
        [PerformedAt] datetime2 NOT NULL,
        [IPAddress] nvarchar(max) NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [InjectionTypes] (
        [Id] int NOT NULL IDENTITY,
        [InjectionName] nvarchar(100) NOT NULL,
        [Unit] nvarchar(50) NOT NULL,
        [Description] nvarchar(500) NULL,
        [DefaultDosage] nvarchar(100) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_InjectionTypes] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [NutritionPackages] (
        [Id] int NOT NULL IDENTITY,
        [PackageName] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [DurationWeeks] int NOT NULL,
        [SessionsPerWeek] int NOT NULL,
        [BasePrice] decimal(18,2) NOT NULL,
        [MaxDiscountPercent] decimal(5,2) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] int NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_NutritionPackages] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [ReservationStatuses] (
        [Id] int NOT NULL IDENTITY,
        [StatusName] nvarchar(50) NOT NULL,
        [Description] nvarchar(255) NULL,
        [ColorCode] nvarchar(7) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ReservationStatuses] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] int NOT NULL IDENTITY,
        [RoleName] nvarchar(50) NOT NULL,
        [Description] nvarchar(255) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [TreatmentTypes] (
        [Id] int NOT NULL IDENTITY,
        [Category] nvarchar(20) NOT NULL,
        [TypeName] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [DefaultCost] decimal(18,2) NULL,
        [DurationMinutes] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TreatmentTypes] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(100) NOT NULL,
        [Email] nvarchar(100) NULL,
        [PhoneNumber] nvarchar(20) NOT NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [Specialization] nvarchar(100) NULL,
        [LicenseNumber] nvarchar(50) NULL,
        [IsActive] bit NOT NULL,
        [LastLoginAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] int NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [VitaminTypes] (
        [Id] int NOT NULL IDENTITY,
        [VitaminName] nvarchar(100) NOT NULL,
        [Formulation] nvarchar(100) NULL,
        [Unit] nvarchar(50) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_VitaminTypes] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [Patients] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NULL,
        [FirstName] nvarchar(50) NOT NULL,
        [LastName] nvarchar(50) NOT NULL,
        [DateOfBirth] datetime2 NULL,
        [Gender] int NULL,
        [BloodType] int NULL,
        [NationalId] nvarchar(20) NULL,
        [HeightCm] decimal(5,2) NULL,
        [Allergies] nvarchar(500) NULL,
        [ChronicDiseases] nvarchar(500) NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] int NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Patients] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Patients_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [RoleId] int NOT NULL,
        [AssignedAt] datetime2 NOT NULL,
        [AssignedBy] int NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [PackageItems] (
        [Id] int NOT NULL IDENTITY,
        [PackageId] int NOT NULL,
        [InjectionId] int NULL,
        [VitaminId] int NULL,
        [Quantity] decimal(10,2) NOT NULL,
        [Unit] nvarchar(50) NULL,
        [WeekNumber] int NOT NULL,
        [Notes] nvarchar(255) NULL,
        CONSTRAINT [PK_PackageItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PackageItems_InjectionTypes_InjectionId] FOREIGN KEY ([InjectionId]) REFERENCES [InjectionTypes] ([Id]),
        CONSTRAINT [FK_PackageItems_NutritionPackages_PackageId] FOREIGN KEY ([PackageId]) REFERENCES [NutritionPackages] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PackageItems_VitaminTypes_VitaminId] FOREIGN KEY ([VitaminId]) REFERENCES [VitaminTypes] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [PatientAddresses] (
        [Id] int NOT NULL IDENTITY,
        [PatientId] int NOT NULL,
        [Street] nvarchar(200) NULL,
        [City] nvarchar(50) NOT NULL,
        [Governorate] nvarchar(50) NULL,
        [PostalCode] nvarchar(10) NULL,
        [IsPrimary] bit NOT NULL,
        CONSTRAINT [PK_PatientAddresses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PatientAddresses_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [PatientNutritionEnrollments] (
        [Id] int NOT NULL IDENTITY,
        [PatientId] int NOT NULL,
        [PackageId] int NOT NULL,
        [DoctorId] int NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [BasePrice] decimal(18,2) NOT NULL,
        [DiscountPercent] decimal(5,2) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [TotalPaid] decimal(18,2) NOT NULL,
        [Notes] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] int NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_PatientNutritionEnrollments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PatientNutritionEnrollments_NutritionPackages_PackageId] FOREIGN KEY ([PackageId]) REFERENCES [NutritionPackages] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PatientNutritionEnrollments_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PatientNutritionEnrollments_Users_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [PatientPhones] (
        [Id] int NOT NULL IDENTITY,
        [PatientId] int NOT NULL,
        [PhoneNumber] nvarchar(20) NOT NULL,
        [PhoneType] nvarchar(20) NOT NULL,
        [IsPrimary] bit NOT NULL,
        CONSTRAINT [PK_PatientPhones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PatientPhones_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [Reservations] (
        [Id] int NOT NULL IDENTITY,
        [PatientId] int NOT NULL,
        [DoctorId] int NOT NULL,
        [StatusId] int NOT NULL,
        [Category] nvarchar(20) NOT NULL,
        [ReservationDate] datetime2 NOT NULL,
        [ReservationTime] time NOT NULL,
        [DurationMinutes] int NOT NULL,
        [Reason] nvarchar(500) NULL,
        [Notes] nvarchar(1000) NULL,
        [IsPaid] bit NOT NULL,
        [TotalAmount] decimal(18,2) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] int NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Reservations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Reservations_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reservations_ReservationStatuses_StatusId] FOREIGN KEY ([StatusId]) REFERENCES [ReservationStatuses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reservations_Users_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [WeeklyFollowUps] (
        [Id] int NOT NULL IDENTITY,
        [EnrollmentId] int NOT NULL,
        [WeekNumber] int NOT NULL,
        [FollowUpDate] datetime2 NOT NULL,
        [WeightKg] decimal(5,2) NULL,
        [HeightCm] decimal(5,2) NULL,
        [BMI] AS CASE WHEN HeightCm > 0 THEN WeightKg / ((HeightCm/100.0) * (HeightCm/100.0)) END,
        [BodyFatPercent] decimal(5,2) NULL,
        [MuscleMassKg] decimal(5,2) NULL,
        [WaistCircumferenceCm] decimal(5,2) NULL,
        [BloodPressureSys] int NULL,
        [BloodPressureDia] int NULL,
        [LabResultsSummary] nvarchar(max) NULL,
        [DoctorNotes] nvarchar(max) NULL,
        [DietCompliance] nvarchar(20) NULL,
        [SideEffects] nvarchar(max) NULL,
        [NextWeekAdjustments] nvarchar(max) NULL,
        [IsCompleted] bit NOT NULL,
        [CompletedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] int NOT NULL,
        CONSTRAINT [PK_WeeklyFollowUps] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WeeklyFollowUps_PatientNutritionEnrollments_EnrollmentId] FOREIGN KEY ([EnrollmentId]) REFERENCES [PatientNutritionEnrollments] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [PatientRecords] (
        [Id] int NOT NULL IDENTITY,
        [PatientId] int NOT NULL,
        [DoctorId] int NOT NULL,
        [ReservationId] int NULL,
        [Category] nvarchar(20) NOT NULL,
        [ChiefComplaint] nvarchar(500) NULL,
        [PresentIllnessHistory] nvarchar(2000) NULL,
        [Diagnosis] nvarchar(1000) NULL,
        [DifferentialDiagnosis] nvarchar(1000) NULL,
        [TreatmentPlan] nvarchar(2000) NULL,
        [Notes] nvarchar(2000) NULL,
        [FollowUpDate] datetime2 NULL,
        [IsLocked] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] int NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_PatientRecords] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PatientRecords_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientRecords_Reservations_ReservationId] FOREIGN KEY ([ReservationId]) REFERENCES [Reservations] ([Id]),
        CONSTRAINT [FK_PatientRecords_Users_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientRecords_Users_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] int NOT NULL IDENTITY,
        [ReservationId] int NULL,
        [PatientId] int NOT NULL,
        [EnrollmentId] int NULL,
        [CollectedBy] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaymentMethod] nvarchar(20) NOT NULL,
        [PaymentDate] datetime2 NOT NULL,
        [ReferenceNumber] nvarchar(100) NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CollectorId] int NOT NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_PatientNutritionEnrollments_EnrollmentId] FOREIGN KEY ([EnrollmentId]) REFERENCES [PatientNutritionEnrollments] ([Id]),
        CONSTRAINT [FK_Payments_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Payments_Reservations_ReservationId] FOREIGN KEY ([ReservationId]) REFERENCES [Reservations] ([Id]),
        CONSTRAINT [FK_Payments_Users_CollectorId] FOREIGN KEY ([CollectorId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [WeeklyAdministeredItems] (
        [Id] int NOT NULL IDENTITY,
        [FollowUpId] int NOT NULL,
        [PackageItemId] int NOT NULL,
        [ActualQuantity] decimal(10,2) NOT NULL,
        [AdministeredBy] int NOT NULL,
        [AdministeredAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        [Notes] nvarchar(500) NULL,
        [PackageItemId1] int NULL,
        CONSTRAINT [PK_WeeklyAdministeredItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WeeklyAdministeredItems_PackageItems_PackageItemId] FOREIGN KEY ([PackageItemId]) REFERENCES [PackageItems] ([Id]),
        CONSTRAINT [FK_WeeklyAdministeredItems_PackageItems_PackageItemId1] FOREIGN KEY ([PackageItemId1]) REFERENCES [PackageItems] ([Id]),
        CONSTRAINT [FK_WeeklyAdministeredItems_Users_AdministeredBy] FOREIGN KEY ([AdministeredBy]) REFERENCES [Users] ([Id]),
        CONSTRAINT [FK_WeeklyAdministeredItems_WeeklyFollowUps_FollowUpId] FOREIGN KEY ([FollowUpId]) REFERENCES [WeeklyFollowUps] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [WeeklyFollowUpLabResults] (
        [Id] int NOT NULL IDENTITY,
        [FollowUpId] int NOT NULL,
        [AnalysisTypeId] int NOT NULL,
        [ResultValue] nvarchar(max) NULL,
        [ReferenceRange] nvarchar(max) NULL,
        [IsNormal] bit NULL,
        [Notes] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_WeeklyFollowUpLabResults] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WeeklyFollowUpLabResults_AnalysisTypes_AnalysisTypeId] FOREIGN KEY ([AnalysisTypeId]) REFERENCES [AnalysisTypes] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_WeeklyFollowUpLabResults_WeeklyFollowUps_FollowUpId] FOREIGN KEY ([FollowUpId]) REFERENCES [WeeklyFollowUps] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [MedicalAnalyses] (
        [Id] int NOT NULL IDENTITY,
        [PatientId] int NOT NULL,
        [DoctorId] int NOT NULL,
        [RecordId] int NULL,
        [AnalysisTypeId] int NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [RequestDate] datetime2 NOT NULL,
        [ResultDate] datetime2 NULL,
        [ResultNotes] nvarchar(2000) NULL,
        [IsUrgent] bit NOT NULL,
        [AnalysisTypeId1] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] int NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_MedicalAnalyses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MedicalAnalyses_AnalysisTypes_AnalysisTypeId] FOREIGN KEY ([AnalysisTypeId]) REFERENCES [AnalysisTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MedicalAnalyses_AnalysisTypes_AnalysisTypeId1] FOREIGN KEY ([AnalysisTypeId1]) REFERENCES [AnalysisTypes] ([Id]),
        CONSTRAINT [FK_MedicalAnalyses_PatientRecords_RecordId] FOREIGN KEY ([RecordId]) REFERENCES [PatientRecords] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MedicalAnalyses_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MedicalAnalyses_Users_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MedicalAnalyses_Users_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [Prescriptions] (
        [Id] int NOT NULL IDENTITY,
        [RecordId] int NOT NULL,
        [MedicationName] nvarchar(200) NOT NULL,
        [Dosage] nvarchar(100) NULL,
        [Frequency] nvarchar(100) NULL,
        [Duration] nvarchar(100) NULL,
        [RouteOfAdministration] nvarchar(50) NULL,
        [Instructions] nvarchar(1000) NULL,
        [IsPrinted] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] int NOT NULL,
        CONSTRAINT [PK_Prescriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Prescriptions_PatientRecords_RecordId] FOREIGN KEY ([RecordId]) REFERENCES [PatientRecords] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [Treatments] (
        [Id] int NOT NULL IDENTITY,
        [RecordId] int NOT NULL,
        [TreatmentTypeId] int NULL,
        [Description] nvarchar(1000) NOT NULL,
        [Cost] decimal(18,2) NULL,
        [PerformedDate] datetime2 NOT NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] int NOT NULL,
        CONSTRAINT [PK_Treatments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Treatments_PatientRecords_RecordId] FOREIGN KEY ([RecordId]) REFERENCES [PatientRecords] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Treatments_TreatmentTypes_TreatmentTypeId] FOREIGN KEY ([TreatmentTypeId]) REFERENCES [TreatmentTypes] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [AnalysisAttachments] (
        [Id] int NOT NULL IDENTITY,
        [AnalysisId] int NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [FilePath] nvarchar(500) NOT NULL,
        [ContentType] nvarchar(50) NULL,
        [FileSizeBytes] bigint NULL,
        [UploadedAt] datetime2 NOT NULL,
        [UploadedBy] int NOT NULL,
        CONSTRAINT [PK_AnalysisAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AnalysisAttachments_MedicalAnalyses_AnalysisId] FOREIGN KEY ([AnalysisId]) REFERENCES [MedicalAnalyses] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE TABLE [PrescriptionAttachments] (
        [Id] int NOT NULL IDENTITY,
        [PrescriptionId] int NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [FilePath] nvarchar(500) NOT NULL,
        [ContentType] nvarchar(50) NULL,
        [FileSizeBytes] bigint NULL,
        [UploadedAt] datetime2 NOT NULL,
        [UploadedBy] int NOT NULL,
        CONSTRAINT [PK_PrescriptionAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PrescriptionAttachments_Prescriptions_PrescriptionId] FOREIGN KEY ([PrescriptionId]) REFERENCES [Prescriptions] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_AnalysisAttachments_AnalysisId] ON [AnalysisAttachments] ([AnalysisId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_MedicalAnalyses_AnalysisTypeId] ON [MedicalAnalyses] ([AnalysisTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_MedicalAnalyses_AnalysisTypeId1] ON [MedicalAnalyses] ([AnalysisTypeId1]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_MedicalAnalyses_CreatedBy] ON [MedicalAnalyses] ([CreatedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_MedicalAnalyses_DoctorId] ON [MedicalAnalyses] ([DoctorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_MedicalAnalyses_PatientId] ON [MedicalAnalyses] ([PatientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_MedicalAnalyses_RecordId] ON [MedicalAnalyses] ([RecordId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_MedicalAnalyses_Status] ON [MedicalAnalyses] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_NutritionPackages_IsActive] ON [NutritionPackages] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_PackageItems_InjectionId] ON [PackageItems] ([InjectionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_PackageItems_PackageId] ON [PackageItems] ([PackageId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_PackageItems_VitaminId] ON [PackageItems] ([VitaminId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_PatientAddresses_PatientId] ON [PatientAddresses] ([PatientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_PatientNutritionEnrollments_DoctorId] ON [PatientNutritionEnrollments] ([DoctorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_PatientNutritionEnrollments_PackageId] ON [PatientNutritionEnrollments] ([PackageId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_PatientNutritionEnrollments_PatientId] ON [PatientNutritionEnrollments] ([PatientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_PatientNutritionEnrollments_Status] ON [PatientNutritionEnrollments] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_PatientPhones_PatientId] ON [PatientPhones] ([PatientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_PatientRecords_CreatedBy] ON [PatientRecords] ([CreatedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_PatientRecords_DoctorId] ON [PatientRecords] ([DoctorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_PatientRecords_PatientId] ON [PatientRecords] ([PatientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PatientRecords_ReservationId] ON [PatientRecords] ([ReservationId]) WHERE [ReservationId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Patients_NationalId] ON [Patients] ([NationalId]) WHERE [NationalId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_Patients_UserId] ON [Patients] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_Payments_CollectorId] ON [Payments] ([CollectorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_Payments_EnrollmentId] ON [Payments] ([EnrollmentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_Payments_PatientId] ON [Payments] ([PatientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_Payments_ReservationId] ON [Payments] ([ReservationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_PrescriptionAttachments_PrescriptionId] ON [PrescriptionAttachments] ([PrescriptionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_Prescriptions_RecordId] ON [Prescriptions] ([RecordId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_Reservations_Category] ON [Reservations] ([Category]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_Reservations_DoctorId] ON [Reservations] ([DoctorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_Reservations_PatientId] ON [Reservations] ([PatientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_Reservations_ReservationDate] ON [Reservations] ([ReservationDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_Reservations_StatusId] ON [Reservations] ([StatusId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReservationStatuses_StatusName] ON [ReservationStatuses] ([StatusName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_RoleName] ON [Roles] ([RoleName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_Treatments_RecordId] ON [Treatments] ([RecordId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_Treatments_TreatmentTypeId] ON [Treatments] ([TreatmentTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserRoles_UserId_RoleId] ON [UserRoles] ([UserId], [RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]) WHERE [Email] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_PhoneNumber] ON [Users] ([PhoneNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_WeeklyAdministeredItems_AdministeredBy] ON [WeeklyAdministeredItems] ([AdministeredBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_WeeklyAdministeredItems_FollowUpId] ON [WeeklyAdministeredItems] ([FollowUpId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_WeeklyAdministeredItems_PackageItemId] ON [WeeklyAdministeredItems] ([PackageItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_WeeklyAdministeredItems_PackageItemId1] ON [WeeklyAdministeredItems] ([PackageItemId1]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_WeeklyFollowUpLabResults_AnalysisTypeId] ON [WeeklyFollowUpLabResults] ([AnalysisTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_WeeklyFollowUpLabResults_FollowUpId] ON [WeeklyFollowUpLabResults] ([FollowUpId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_WeeklyFollowUps_EnrollmentId] ON [WeeklyFollowUps] ([EnrollmentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    CREATE INDEX [IX_WeeklyFollowUps_FollowUpDate] ON [WeeklyFollowUps] ([FollowUpDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260620221656_InitialMigrationCreateDBwuthDbSeed'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260620221656_InitialMigrationCreateDBwuthDbSeed', N'8.0.28');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622204648_fixingCollectedByColumnInPaymentEntity'
)
BEGIN
    ALTER TABLE [Payments] DROP CONSTRAINT [FK_Payments_Users_CollectorId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622204648_fixingCollectedByColumnInPaymentEntity'
)
BEGIN
    DROP INDEX [IX_Payments_CollectorId] ON [Payments];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622204648_fixingCollectedByColumnInPaymentEntity'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Payments]') AND [c].[name] = N'CollectorId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Payments] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [Payments] DROP COLUMN [CollectorId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622204648_fixingCollectedByColumnInPaymentEntity'
)
BEGIN
    CREATE INDEX [IX_Payments_CollectedBy] ON [Payments] ([CollectedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622204648_fixingCollectedByColumnInPaymentEntity'
)
BEGIN
    ALTER TABLE [Payments] ADD CONSTRAINT [FK_Payments_Users_CollectedBy] FOREIGN KEY ([CollectedBy]) REFERENCES [Users] ([Id]) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622204648_fixingCollectedByColumnInPaymentEntity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260622204648_fixingCollectedByColumnInPaymentEntity', N'8.0.28');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260629192042_AddWeightToPatientEntity'
)
BEGIN
    ALTER TABLE [Patients] ADD [weight] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260629192042_AddWeightToPatientEntity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260629192042_AddWeightToPatientEntity', N'8.0.28');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260629211110_correctWeightColumnName'
)
BEGIN
    EXEC sp_rename N'[Patients].[weight]', N'Weight', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260629211110_correctWeightColumnName'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260629211110_correctWeightColumnName', N'8.0.28');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702154016_addUpdatedByandUpdateAttoWeeklyFollowUp'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260702154016_addUpdatedByandUpdateAttoWeeklyFollowUp', N'8.0.28');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    ALTER TABLE [Reservations] ADD [ClinicId] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    ALTER TABLE [Payments] ADD [CancellationReason] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    ALTER TABLE [Payments] ADD [CancelledAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    ALTER TABLE [Payments] ADD [CancelledBy] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    ALTER TABLE [Payments] ADD [ClinicId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    ALTER TABLE [Payments] ADD [ClinicNetAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    ALTER TABLE [Payments] ADD [DeductionPercentage] decimal(5,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    ALTER TABLE [Payments] ADD [IsFirstVisitDeduction] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    ALTER TABLE [Payments] ADD [LastModifiedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    ALTER TABLE [Payments] ADD [LastModifiedBy] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    ALTER TABLE [Payments] ADD [OriginalAmount] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    ALTER TABLE [Payments] ADD [PatientSourceId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    ALTER TABLE [Payments] ADD [SourceDeductionAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    ALTER TABLE [Payments] ADD [Status] nvarchar(20) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    ALTER TABLE [Patients] ADD [PatientSourceId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    CREATE TABLE [Clinics] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Address] nvarchar(300) NULL,
        [Phone] nvarchar(30) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] int NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Clinics] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    CREATE TABLE [PatientSources] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [DefaultDeductionPercentage] decimal(5,2) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] int NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_PatientSources] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    CREATE TABLE [PaymentAdjustments] (
        [Id] int NOT NULL IDENTITY,
        [PaymentId] int NOT NULL,
        [ActionType] nvarchar(30) NOT NULL,
        [OldAmount] decimal(18,2) NULL,
        [NewAmount] decimal(18,2) NULL,
        [Reason] nvarchar(500) NULL,
        [PerformedBy] int NOT NULL,
        [PerformedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentAdjustments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentAdjustments_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Payments] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    CREATE TABLE [ClinicSourceAgreements] (
        [Id] int NOT NULL IDENTITY,
        [ClinicId] int NOT NULL,
        [PatientSourceId] int NOT NULL,
        [DeductionPercentage] decimal(5,2) NOT NULL,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] int NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_ClinicSourceAgreements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ClinicSourceAgreements_Clinics_ClinicId] FOREIGN KEY ([ClinicId]) REFERENCES [Clinics] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ClinicSourceAgreements_PatientSources_PatientSourceId] FOREIGN KEY ([PatientSourceId]) REFERENCES [PatientSources] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    CREATE INDEX [IX_Reservations_ClinicId] ON [Reservations] ([ClinicId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    CREATE INDEX [IX_Payments_ClinicId] ON [Payments] ([ClinicId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    CREATE INDEX [IX_Payments_PatientSourceId] ON [Payments] ([PatientSourceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    CREATE INDEX [IX_Payments_Status] ON [Payments] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    CREATE INDEX [IX_Patients_PatientSourceId] ON [Patients] ([PatientSourceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Clinics_Name] ON [Clinics] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ClinicSourceAgreements_ClinicId_PatientSourceId] ON [ClinicSourceAgreements] ([ClinicId], [PatientSourceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    CREATE INDEX [IX_ClinicSourceAgreements_PatientSourceId] ON [ClinicSourceAgreements] ([PatientSourceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PatientSources_Name] ON [PatientSources] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    CREATE INDEX [IX_PaymentAdjustments_PaymentId] ON [PaymentAdjustments] ([PaymentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    ALTER TABLE [Patients] ADD CONSTRAINT [FK_Patients_PatientSources_PatientSourceId] FOREIGN KEY ([PatientSourceId]) REFERENCES [PatientSources] ([Id]) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    ALTER TABLE [Payments] ADD CONSTRAINT [FK_Payments_Clinics_ClinicId] FOREIGN KEY ([ClinicId]) REFERENCES [Clinics] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    ALTER TABLE [Payments] ADD CONSTRAINT [FK_Payments_PatientSources_PatientSourceId] FOREIGN KEY ([PatientSourceId]) REFERENCES [PatientSources] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    ALTER TABLE [Reservations] ADD CONSTRAINT [FK_Reservations_Clinics_ClinicId] FOREIGN KEY ([ClinicId]) REFERENCES [Clinics] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806195047_AddClinicSourcePaymentEnhancements'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260806195047_AddClinicSourcePaymentEnhancements', N'8.0.28');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810120000_MoveTreatmentTypeToReservation'
)
BEGIN
    ALTER TABLE [Treatments] DROP CONSTRAINT [FK_Treatments_TreatmentTypes_TreatmentTypeId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810120000_MoveTreatmentTypeToReservation'
)
BEGIN
    DROP INDEX [IX_Treatments_TreatmentTypeId] ON [Treatments];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810120000_MoveTreatmentTypeToReservation'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Treatments]') AND [c].[name] = N'TreatmentTypeId');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Treatments] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Treatments] DROP COLUMN [TreatmentTypeId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810120000_MoveTreatmentTypeToReservation'
)
BEGIN
    ALTER TABLE [Reservations] ADD [TreatmentTypeId] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810120000_MoveTreatmentTypeToReservation'
)
BEGIN
    CREATE INDEX [IX_Reservations_TreatmentTypeId] ON [Reservations] ([TreatmentTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810120000_MoveTreatmentTypeToReservation'
)
BEGIN
    ALTER TABLE [Reservations] ADD CONSTRAINT [FK_Reservations_TreatmentTypes_TreatmentTypeId] FOREIGN KEY ([TreatmentTypeId]) REFERENCES [TreatmentTypes] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810120000_MoveTreatmentTypeToReservation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260810120000_MoveTreatmentTypeToReservation', N'8.0.28');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904150116_FixingPrescription'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Prescriptions]') AND [c].[name] = N'Dosage');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Prescriptions] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [Prescriptions] DROP COLUMN [Dosage];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904150116_FixingPrescription'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Prescriptions]') AND [c].[name] = N'Duration');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Prescriptions] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [Prescriptions] DROP COLUMN [Duration];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904150116_FixingPrescription'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Prescriptions]') AND [c].[name] = N'Frequency');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Prescriptions] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [Prescriptions] DROP COLUMN [Frequency];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904150116_FixingPrescription'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Prescriptions]') AND [c].[name] = N'MedicationName');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [Prescriptions] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [Prescriptions] DROP COLUMN [MedicationName];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904150116_FixingPrescription'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Prescriptions]') AND [c].[name] = N'RouteOfAdministration');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Prescriptions] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [Prescriptions] DROP COLUMN [RouteOfAdministration];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904150116_FixingPrescription'
)
BEGIN
    EXEC sp_rename N'[Prescriptions].[Instructions]', N'Notes', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904150116_FixingPrescription'
)
BEGIN
    ALTER TABLE [Prescriptions] ADD [PatientRecordId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904150116_FixingPrescription'
)
BEGIN
    ALTER TABLE [Prescriptions] ADD [PrescriptionDate] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904150116_FixingPrescription'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PrescriptionAttachments]') AND [c].[name] = N'ContentType');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [PrescriptionAttachments] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [PrescriptionAttachments] ALTER COLUMN [ContentType] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904150116_FixingPrescription'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Patients]') AND [c].[name] = N'Weight');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [Patients] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [Patients] ALTER COLUMN [Weight] decimal(10,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904150116_FixingPrescription'
)
BEGIN
    CREATE TABLE [PrescriptionItems] (
        [Id] int NOT NULL IDENTITY,
        [PrescriptionId] int NOT NULL,
        [MedicationName] nvarchar(200) NOT NULL,
        [Dosage] nvarchar(100) NULL,
        [Frequency] nvarchar(100) NULL,
        [Duration] nvarchar(100) NULL,
        [RouteOfAdministration] nvarchar(50) NULL,
        [Instructions] nvarchar(1000) NULL,
        CONSTRAINT [PK_PrescriptionItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PrescriptionItems_Prescriptions_PrescriptionId] FOREIGN KEY ([PrescriptionId]) REFERENCES [Prescriptions] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904150116_FixingPrescription'
)
BEGIN
    CREATE INDEX [IX_Prescriptions_PatientRecordId] ON [Prescriptions] ([PatientRecordId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904150116_FixingPrescription'
)
BEGIN
    CREATE INDEX [IX_PrescriptionItems_PrescriptionId] ON [PrescriptionItems] ([PrescriptionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904150116_FixingPrescription'
)
BEGIN
    ALTER TABLE [Prescriptions] ADD CONSTRAINT [FK_Prescriptions_PatientRecords_PatientRecordId] FOREIGN KEY ([PatientRecordId]) REFERENCES [PatientRecords] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904150116_FixingPrescription'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260904150116_FixingPrescription', N'8.0.28');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906120000_SeedNutritionFollowUpTreatmentType'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM [TreatmentTypes] WHERE [TypeName] = N'Follow-up' AND [Category] = N'Nutritional')
    BEGIN
        INSERT INTO [TreatmentTypes] ([Category], [TypeName], [Description], [DefaultCost], [DurationMinutes], [IsActive], [CreatedAt])
        VALUES (N'Nutritional', N'Follow-up', N'Nutrition follow-up visit — included in the enrollment package, no extra charge', 0.00, 15, 1, GETUTCDATE());
    END

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260906120000_SeedNutritionFollowUpTreatmentType'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260906120000_SeedNutritionFollowUpTreatmentType', N'8.0.28');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911201826_AddPaymentIndex'
)
BEGIN
    CREATE INDEX [IX_Payments_Status_PaymentDate] ON [Payments] ([Status], [PaymentDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911201826_AddPaymentIndex'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260911201826_AddPaymentIndex', N'8.0.28');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911203006_AddReservationIndex'
)
BEGIN
    CREATE INDEX [IX_Reservations_DoctorId_ReservationDate_ReservationTime_Id] ON [Reservations] ([DoctorId], [ReservationDate], [ReservationTime], [Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911203006_AddReservationIndex'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260911203006_AddReservationIndex', N'8.0.28');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260917200043_AddPatientQueueFields'
)
BEGIN
    ALTER TABLE [Reservations] ADD [CheckedInAtUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260917200043_AddPatientQueueFields'
)
BEGIN
    ALTER TABLE [Reservations] ADD [ConsultationStartedAtUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260917200043_AddPatientQueueFields'
)
BEGIN
    ALTER TABLE [Reservations] ADD [QueueEndedAtUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260917200043_AddPatientQueueFields'
)
BEGIN
    ALTER TABLE [Reservations] ADD [QueueStatus] nvarchar(20) NOT NULL DEFAULT N'NotCheckedIn';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260917200043_AddPatientQueueFields'
)
BEGIN
    CREATE INDEX [IX_Reservations_ClinicId_DoctorId_QueueStatus_CheckedInAtUtc] ON [Reservations] ([ClinicId], [DoctorId], [QueueStatus], [CheckedInAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260917200043_AddPatientQueueFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260917200043_AddPatientQueueFields', N'8.0.28');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919210058_AddPatientFollowUpTracking'
)
BEGIN
    ALTER TABLE [PatientRecords] ADD [FollowUpDismissalReason] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919210058_AddPatientFollowUpTracking'
)
BEGIN
    ALTER TABLE [PatientRecords] ADD [FollowUpReservationId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919210058_AddPatientFollowUpTracking'
)
BEGIN
    ALTER TABLE [PatientRecords] ADD [FollowUpStatus] nvarchar(20) NOT NULL DEFAULT N'Pending';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919210058_AddPatientFollowUpTracking'
)
BEGIN
    ALTER TABLE [PatientRecords] ADD [FollowUpUpdatedAtUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919210058_AddPatientFollowUpTracking'
)
BEGIN
    ALTER TABLE [PatientRecords] ADD [FollowUpUpdatedBy] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919210058_AddPatientFollowUpTracking'
)
BEGIN
    CREATE INDEX [IX_PatientRecords_FollowUpReservationId] ON [PatientRecords] ([FollowUpReservationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919210058_AddPatientFollowUpTracking'
)
BEGIN
    CREATE INDEX [IX_PatientRecords_FollowUpStatus_FollowUpDate_DoctorId] ON [PatientRecords] ([FollowUpStatus], [FollowUpDate], [DoctorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919210058_AddPatientFollowUpTracking'
)
BEGIN
    CREATE INDEX [IX_PatientRecords_FollowUpUpdatedBy] ON [PatientRecords] ([FollowUpUpdatedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919210058_AddPatientFollowUpTracking'
)
BEGIN
    ALTER TABLE [PatientRecords] ADD CONSTRAINT [FK_PatientRecords_Reservations_FollowUpReservationId] FOREIGN KEY ([FollowUpReservationId]) REFERENCES [Reservations] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919210058_AddPatientFollowUpTracking'
)
BEGIN
    ALTER TABLE [PatientRecords] ADD CONSTRAINT [FK_PatientRecords_Users_FollowUpUpdatedBy] FOREIGN KEY ([FollowUpUpdatedBy]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919210058_AddPatientFollowUpTracking'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260919210058_AddPatientFollowUpTracking', N'8.0.28');
END;
GO

COMMIT;
GO

