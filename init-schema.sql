-- Script d'initialisation du schema dbo et de la table __EFMigrationsHistory
-- Ce script doit etre execute avant la premiere migration EF Core

CREATE SCHEMA IF NOT EXISTS dbo;

CREATE TABLE IF NOT EXISTS dbo."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);
