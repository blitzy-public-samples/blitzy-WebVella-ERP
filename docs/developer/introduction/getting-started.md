<!--{"sort_order":2, "name": "getting-started", "label": "Getting started"}-->
# Getting started

## Setting up environment
 
1. We use Windows 10 and Windows 2012 Server as development operating systems. Update your windows before continue installing required software. 
*Other operating systems - when we get more experience we will extend this document.*
 
2. The WebVella ERP project is a set of .NET 9.0 application and libraries. 
If you want to run, develop and extend it, first you need .NET 9.0 framework installed.
At the moment of creating this document we use .NET version 9.0.
Go to https://dotnet.microsoft.com/download download latest SDK and Runtime and install both.

3. You will also need the Microsoft Visual Studio to build and run the project. 
We use Visual Studio 2022 and it can be downloaded from https://visualstudio.microsoft.com/downloads/ . 
Note its free for open-source contributors like ourself. Once its installed, check notifications panel for updates and install them.

4. WebVella ERP use PotgreSQL database. You can go download the latest version from https://www.postgresql.org/download/ . 
If you don't have any management tool for PostgreSQL install also pgAdmin. You can download it from https://www.pgadmin.org/download/ . 

## Start with seed project

1. Download or Clone the repository sources ($ git clone https://github.com/WebVella/WebVella-ERP-Seed.git ) .
This project contains ready to start web site project with all ERP libraries and resources installed from nuget packages.

2. Create an empty PostgreSQL database for the erp. When erp starts for first time, it will create 
necessary structure and data.

3. Edit the Config.json file and set the correct sql connection string. 

4. Run the project.

## Get, build and run the sources

1. Download or Clone the repository sources ($ git clone https://github.com/WebVella/WebVella-ERP.git) .

2. Create an empty PostgreSQL database for the erp. When erp starts for first time, it will create 
necessary structure and data.

3. Open the solution file WebVella.ERP3.sln with Visual Studio and build the solution. 
Set as startup  (if is not already set). Edit the Config.json file 
in project WebVella.Erp.Site and set the correct sql connection string. 
You may also want to change other configuration values (like timezone), but is not required.

4. Run the application. The project is configured to open browser open on login screen when started. 
All erp functionality requires authentication. Use the default account email "**erp@webvella.com**" and password "**erp**"
to authenticate. 

5. If you want to develop your own components or applications, be sure that the WebVella.Erp.Plugins.SDK is included in your solution.
This application will help you greatly to create and manage ERP objects like Entities and Relations, although its not required because
ERP API can used directly to create and manage these objects.

## Docker-based setup (alternative)

A `Dockerfile` and `docker-compose.yml` have been added to the repository root for a containerized setup. This approach handles PostgreSQL setup automatically — no manual database creation is required.

Quick start:

```bash
docker compose up -d --build
```

Once the containers are running, poll the health endpoint until the application is ready:

```bash
curl -sf http://localhost:5000/api/v3/en_US/meta
```

For detailed Docker setup instructions, including environment configuration and troubleshooting, see the [Docker Environment Setup](../../security/docker-setup.md) documentation.

## Security Assessment

A comprehensive security validation workflow has been documented for WebVella ERP, covering dynamic application security testing (DAST) with OWASP ZAP and Nuclei scanners.

The security documentation covers:
- Docker environment setup for security scanning
- JWT authentication for authenticated scans
- OWASP ZAP and Nuclei scan configuration
- Finding analysis, deduplication, and triage
- ASP.NET Core 9 remediation patterns
- Final security assessment reporting with CWE references

See the [Security Assessment Overview](../../security/README.md) for the complete security validation workflow documentation.

