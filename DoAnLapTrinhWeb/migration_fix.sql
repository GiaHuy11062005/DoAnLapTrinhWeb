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
CREATE TABLE [Cities] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NULL,
    [ApiCode] int NOT NULL,
    CONSTRAINT [PK_Cities] PRIMARY KEY ([Id])
);

CREATE TABLE [SearchLogs] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NULL,
    [CityId] int NULL,
    [DistrictId] int NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_SearchLogs] PRIMARY KEY ([Id])
);

CREATE TABLE [Users] (
    [Id] int NOT NULL IDENTITY,
    [FullName] nvarchar(max) NULL,
    [Email] nvarchar(max) NULL,
    [PasswordHash] nvarchar(max) NULL,
    [Role] nvarchar(max) NULL,
    [IsEmailConfirmed] bit NOT NULL,
    [EmailConfirmationToken] nvarchar(max) NULL,
    [CityId] int NULL,
    [DistrictId] int NULL,
    [Phone] nvarchar(max) NULL,
    [UserIdentity] nvarchar(max) NULL,
    [DestinationAddress] nvarchar(max) NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);

CREATE TABLE [Districts] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NULL,
    [ApiCode] int NOT NULL,
    [CityId] int NOT NULL,
    CONSTRAINT [PK_Districts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Districts_Cities_CityId] FOREIGN KEY ([CityId]) REFERENCES [Cities] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Accommodations] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(max) NULL,
    [Description] nvarchar(max) NULL,
    [Price] decimal(18,0) NOT NULL,
    [Area] float NOT NULL,
    [SpecificAddress] nvarchar(max) NULL,
    [CityId] int NOT NULL,
    [DistrictId] int NOT NULL,
    [AllowsPets] bit NOT NULL,
    [HasParking] bit NOT NULL,
    [ContactPhone] nvarchar(max) NULL,
    [IsApproved] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [OwnerId] int NOT NULL,
    CONSTRAINT [PK_Accommodations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Accommodations_Cities_CityId] FOREIGN KEY ([CityId]) REFERENCES [Cities] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Accommodations_Districts_DistrictId] FOREIGN KEY ([DistrictId]) REFERENCES [Districts] ([Id]),
    CONSTRAINT [FK_Accommodations_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AccommodationImages] (
    [Id] int NOT NULL IDENTITY,
    [ImageUrl] nvarchar(max) NULL,
    [AccommodationId] int NOT NULL,
    CONSTRAINT [PK_AccommodationImages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AccommodationImages_Accommodations_AccommodationId] FOREIGN KEY ([AccommodationId]) REFERENCES [Accommodations] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Favorites] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [AccommodationId] int NOT NULL,
    CONSTRAINT [PK_Favorites] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Favorites_Accommodations_AccommodationId] FOREIGN KEY ([AccommodationId]) REFERENCES [Accommodations] ([Id]),
    CONSTRAINT [FK_Favorites_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
);

CREATE INDEX [IX_AccommodationImages_AccommodationId] ON [AccommodationImages] ([AccommodationId]);

CREATE INDEX [IX_Accommodations_CityId] ON [Accommodations] ([CityId]);

CREATE INDEX [IX_Accommodations_DistrictId] ON [Accommodations] ([DistrictId]);

CREATE INDEX [IX_Accommodations_OwnerId] ON [Accommodations] ([OwnerId]);

CREATE INDEX [IX_Districts_CityId] ON [Districts] ([CityId]);

CREATE INDEX [IX_Favorites_AccommodationId] ON [Favorites] ([AccommodationId]);

CREATE INDEX [IX_Favorites_UserId] ON [Favorites] ([UserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260610031404_InitialCreate', N'10.0.8');

COMMIT;
GO

