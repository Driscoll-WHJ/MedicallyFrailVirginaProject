Copyright (c) 2005-2026 Teradata Corporation. All rights reserved.

--------------------------------------------------------------------------------
                     .NET Data Provider for Teradata
                              Version 20.0.9
--------------------------------------------------------------------------------

********************************************************************************
****  NOTE: The Data Provider requires .NET FRAMEWORK 4.6.2 or higher,      ****
****        or .NET 6 or higher.                                            ****
****        It does not support .NET Framework versions 3.5 through 4.6.1.  ****
********************************************************************************

********************************************************************************
****  NOTE: The connection string attribute UseEnhancedSchemaTable has      ****
****        been deprecated. It may be removed in a future release.         ****
********************************************************************************


RELEASE NOTES


Content:

1.0 Overview
1.1   New Features
1.2   Problems Fixed
2.0 Teradata Product Dependencies
3.0 Operating Environments
4.0 Installation
4.1   Installation Procedure
4.2   Installation Options
5.0 Uninstall
6.0 Restrictions and Known Issues


1.0 Overview
============
The .NET Data Provider for Teradata is an implementation of the Microsoft 
ADO.NET specification. It provides direct access to the Teradata Vantage 
and integrates with the DataSet. .NET Applications use the Data Provider
to load data into or retrieve data from the Advanced SQL Engine.

The .NET Data Provider for Teradata is available for download as a NuGet 
package (from https://www.nuget.org/packages/Teradata.Client.Provider) and a
MSI package (from https://downloads.teradata.com).

The MSI package installs the .NET Data Provider for Teradata (ADO.NET) and 
the Entity Framework Provider for Teradata with support for the Entity Data 
Model (EDM), LINQ-to-Entities and Entity-SQL. The Entity Framework Provider 
for Teradata supports ADO.NET Entity Framework 3.5 SP1 features. It does not
support ADO.NET Entity Framework 4.0 (or higher) features and it is not 
compatible with Entity Framework 6.x or Entity Framework Core. The assemblies
included in the MSI package are .NET Framework assemblies. The package does 
not include the assemblies for .NET Core.

The NuGet package contains the Data Provider for .NET Framework and .NET Core.
This package does not include the Entity Framework Provider or the Entity 
Framework Core Provider. 

The Entity Framework Core Provider for Teradata is available as a NuGet package
(from https://www.nuget.org/packages/Teradata.EntityFrameworkCore).

See following links for additional information:
 a) ADO.NET Overview: 
      https://docs.microsoft.com/en-us/dotnet/framework/data/adonet/ado-net-overview

 b) Entity Framework:
      https://docs.microsoft.com/en-us/ef/


1.1 New Features
================

Version 20.0.2:
  a) Added Proxy Server support.

Version 20.0.3:
  b) Added OpenID Connect (Client Credentials Grant with JWT Bearer Token) and
     CODE (Device Code Flow, a.k.a. Device Authorization Grant)
     AuthenticationMechanism support.

Version 20.0.4:
  c) Added support for FIPS mode via the EnableFIPS connection string attribute.

Version 20.0.5:
  d) Added a Token Cache to improve the user experience with EXTERNALBROWSER 
     and CODE authentication mechanisms.

  e) Enabled support for TLS 1.3 via the SslProtocol connection string attribute.

Version 20.0.6:
  f) Enabled the use of SslProtocol and SslCRCTimeout connection string 
     attributes in OpenID Connect connections to Identity Providers.

  g) Added connection-related Client Attributes.

  h) Added a new connection string attribute UseLocalCatalog to enable retrieval
     of Open Table Format object metadata.

  i) Updated package dependencies used in OpenID Connect functionality.

  j) Added support for the BEARER authentication mechanism in Visual Studio
     Integration.

Version 20.0.8:
  k) Added OidcRefresh and OidcRefreshPercent connection string attributes for
     OpenID Connect connections to Identity Providers.

  l) Added OpenID Connect CRED (Client Credentials Grant with Client Secret)
     AuthenticationMechanism support.

Version 20.0.9:
  m) Added OidcRedirectPort connection string attribute for EXTERNALBROWSER
     connections.


1.2 Problems Fixed
==================

Version 20.0.3:
  a) Fixed the use of IP address literals in proxy settings.

  b) Fixed a failure to load local certificates when using SslCA or SslCAPath.

Version 20.0.4:
  c) Fixed a "Safe handle has been closed" exception during multi-threaded 
     session establishment.

Version 20.0.5:
  d) Improved error message for the RemoteCertificateNameMismatch error.

  e) Fixed a connection failure resulting from incorrectly processed proxy 
     server response.

Version 20.0.6:
  f) Fixed the Windows installation package to install additional transitory
     dependency assemblies into the Global Assembly Cache.

Version 20.0.7:
  g) Added a null check before referencing the system proxy.

Version 20.0.8:
  h) Removed unnecessary reverse DNS lookup for connections using IP address
     literals in DataSource.


2.0 Teradata Product Dependencies
=================================
The Data Provider communicates directly to the Advanced SQL Engine. There are no
dependencies on any of the Teradata Tools and Utilities products.


3.0 Operating Environments
===========================
The following operating environments are supported:

Client Operating Systems:
    Microsoft Windows 10              x86 & x64 Editions
    Microsoft Windows 11
    Microsoft Windows Server 2016, 2019, 2022, 2025
    CentOS 7.x, 8.x, 9.x
    Red Hat Enterprise Linux 7.x, 8.x, 9.x
    SUSE Linux Enterprise 12, 15
    Ubuntu 18.04, 20.04, 22.04, 24.04
    macOS 10.15, 11.x, 12.x, 13.x, 14.x, 15.x

Client Operating Environment:
    Microsoft .NET Framework 4.6.2    x86 & x64 (CLR 4.0)
    Microsoft .NET Framework 4.7.x    x86 & x64 (CLR 4.0)
    Microsoft .NET Framework 4.8.x    x86 & x64 (CLR 4.0)
    Microsoft .NET 6                  Windows, Linux, macOS
    Microsoft .NET 7                  Windows, Linux, macOS
    Microsoft .NET 8                  Windows, Linux, macOS
                    
Microsoft Visual Studio:
    2015 Professional or higher edition
    2017 Professional or higher edition
    2019 Professional or higher edition
    2022 Professional or higher edition

The .NET Data Provider for Teradata and the Entity Framework Provider 
support the following Teradata Database/Advanced SQL Engine Releases:
    17.0
    17.10
    17.20
    20.0
        
The Entity Framework Provider for Teradata supports ADO.NET Entity 
Framework 3.5 SP1 features. It does not support ADO.NET Entity Framework 4.0 
(or higher) features. The Entity Framework Provider is not compatible with 
the Entity Framework 6.x or Entity Framework Core. 

The .NET Data Provider for Teradata architecture is MSIL. Therefore a 
single installation supports 32-bit and 64-bit applications.


4.0 Installation
================
For step-by-step Installation Instructions refer to "Teradata Tools and 
Utilities Installation Guide for Microsoft Windows" document on 
https://docs.teradata.com/. 


4.1 Installation Procedure
==========================
The .NET Data Provider for Teradata is dependent on the Microsoft .NET Framework
version 4.6.2 which can be downloaded from:
    https://dotnet.microsoft.com/en-us/download/dotnet-framework

Alternatively you may use the .NET implementation of the Provider. This is
dependent of Microsoft .NET 6 or newer which can be downloaded from:
    https://dotnet.microsoft.com/en-us/download

*** RECOMMENDATION: Uninstall any previous version(s) of the .NET Data Provider
for Teradata if there is no application dependency.

In the following instructions, the "installation executable" refers to the 
the file:

     tdnetdp__windows_indep.20.00.09.00.exe

NOTE:
      The name of the installation package obtained when performing 
      a Network Install from the TTU media is setup.exe.   


Any reference to the .NET Data Provider for Teradata or the Teradata Provider
refers to

     .NET Data Provider for Teradata 20.00.09.00

Follow this procedure to install the Data Provider:

   1- Run installation executable.   (See NOTE below.)
   
   2- In the Choose Setup Language dialog, select the Language you want to use
      and click OK.
      
   3- When the Welcome screen appears, click Next>.
   
   4- In the Choose Destination folder dialog, choose a destination folder 
      and click Next>.

   5- In the Setup Type dialog, choose either complete or custom setup type
      and click Next>. Choosing 'complete' setup type would select all the 
      features for installation whereas choosing 'custom' setup type allows 
      the user to select features for installation.

   6- In the Custom setup dialog, click Next> to accept the defaults. 
      The "Integrate the Data Provider with Microsoft Visual Studio(s)" feature 
      is visible when any of the Microsoft Visual Studio versions supported by
      this version of the .NET Data Provider for Teradata is installed. 
      
   7- In the Ready to install dialog, click on 'Install' to start the package 
      installation. If Visual studio features are selected then this step will 
      take several minutes to complete.      
 
   8- In the InstallShield Wizard Complete screen, click Finish.   
   

4.2 Installation options
=========================
Interactive install:
   Double click installation executable to start the installation or Run 
   the installation executable from command prompt and go through the dialog 
   sequence.

Silent install:
  From command prompt:   
    To install the full package specify the installation executable with the
    following default options:

       /s /v"/qn"   

    To install to a user defined location run specify the following options:

       /s /v"/qn INSTALLDIR=c:\test"

    To install the package without Visual Studio features use the options:

       /s /v"/qn VSFEATURE=0"    

NOTE:
   The self-extracting installation package, downloaded from the web
   (downloads.teradata.com), is not compatible with the SYSTEM account.
   The SETUP.EXE installation package, available on the Teradata Tools and
   Utilities media, must be used to deploy the Data Provider with the SYSTEM
   account.

Installation through SMS

   Extract the msi file from the installation executable by running the 
   following command at command prompt
   
        "installation executable" /a

   Using the extracted '.NET Data Provider for Teradata.msi' file the package 
   can be pushed from SMS server to SMS clients using the following commands:

   To install full package with defaults use
   msiexec /i ".NET Data Provider for Teradata.msi" /qn        

   To install to a user defined location use
   msiexec /i ".NET Data Provider for Teradata.msi" INSTALLDIR=c:\test /qn

   To install the package without Visual Studio features use
   msiexec /i ".NET Data Provider for Teradata.msi" VSFEATURE=0 /qn


5.0 Uninstall
=============
Follow this procedure to uninstall the .NET Data Provider for Teradata: 

  1- Open "Control Panel".

  2- Start "Add or Remove Programs".

  3- Find ".NET Data Provider for Teradata" and click Remove.
     This will uninstall the package.


6.0 Restrictions and Known Issues
=================================
a) The .NET Data Provider for Teradata requires Full-Trust permission-set to 
   run. 
