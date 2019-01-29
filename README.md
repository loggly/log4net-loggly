log4net-loggly
==============

Custom log4net appenders for importing logging events to loggly. It’s asynchronous and will send logs in the background without blocking your application. Check out Loggly's [.Net logging documentation](https://www.loggly.com/docs/net-logs/) to learn more.

**Note:** This library supports both .NET 4.0 and .NET Standard 2.0. Please see the section **[.NET Core Support](README.md#net-core-support)** below.

Download log4net-loggly package from NuGet. Use the following command.

    Install-Package log4net-loggly

Add the following code in your web.config to configure LogglyAppender in your application
```xml
<configSections>
    <section name="log4net" type="log4net.Config.Log4NetConfigurationSectionHandler, log4net" />
</configSections>
<log4net>
    <root>
        <level value="ALL" />
        <appender-ref ref="LogglyAppender" />
    </root>
    <appender name="LogglyAppender" type="log4net.loggly.LogglyAppender, log4net-loggly">
        <rootUrl value="https://logs-01.loggly.com/" />
        <customerToken value="your-customer-token" />
        <tag value="your-custom-tags,separated-by-comma" />
        <logicalThreadContextKeys value="lkey1,lkey2" /> <!-- optional -->
        <globalContextKeys value="gkey1,gkey2" /> <!-- optional -->
    </appender>
</log4net>
```

If you want to append **GlobalContext** and/or **LogicalThreadContext** properties to your log you need to define the list of context properties in the configuration. 

For GlobalContext Properties use `<globalContextKeys value="gkey1,gkey2" />`

For LogicalThreadContext Properties `<logicalThreadContextKeys value="lkey1,lkey2" />`


You can also use **layout** to render logs according to your Pattern Layouts
```xml
<layout type="log4net.Layout.PatternLayout">
    <conversionPattern value="%date [%thread] %-5level %logger %message" />
</layout>
```

Add the following entry to your AssemblyInfo.cs
```csharp
[assembly: log4net.Config.XmlConfigurator(Watch = true)]
```

Alternatively, you can add the following code in your Main method or in Global.asax file
```csharp
log4net.Config.XmlConfigurator.Configure();
```

Create an object of the Log class using LogManager
```csharp
var logger = LogManager.GetLogger(typeof(Class));
```
    
Send logs to Loggly using the following code
```csharp
// send plain string
logger.Info("log message");

// send an exception
logger.Error("your log message", new Exception("your exception message"));

// send dictionary as JSON object
var items = new Dictionary<string,string>();
items.Add("key1","value1");
items.Add("key2", "value2");
logger.Info(items);

// send any object as JSON object
logger.Debug(new { Property = "This is anonymous object", Property2 = "with two properties" });
```

## Flushing logs on application shutdown

Library is buffering and sending log messages to Loggly asynchronously in background. That means that some logs may be still in buffer when the application terminates. To make sure that all logs have been sent you need to cleanly shutdown log4net logger using following code:
```csharp
logger.Logger.Repository.Shutdown();
```
This flushes any pending messages.

## Advanced configuration

By default, library uses Loggly `/bulk` end point (https://www.loggly.com/docs/http-bulk-endpoint/). To use `/inputs` endpoint, add the following configuration to config file to `<appender>` section

```xml
<logMode value="inputs" />
```

Library by default serializes and sends 4 levels of inner exceptions in case of warn/error log. If you want to change this number just add following configuration to config file to `<appender>` section
```xml
<numberOfInnerExceptions value value="10"/>
```

### .NET Core Support:

**Prerequisites:**

- Since this library support .NET Core target framework 2.0, make sure you are using either version 15.3.0 or higher of Visual Studio IDE 2017 or Visual Studio Code.

- You must have installed the .NET Core 2.0 SDK and Runtime environment to develop and run your .NET Core 2.0 applications.

- You may also have to install the .NET Core cross-platform development workload (in the Other Toolsets section). Please see the more details [here](https://docs.microsoft.com/en-us/dotnet/core/windows-prerequisites?tabs=netcore2x).

Once you are done with the environment setup, now you are all set to create your application in .NET Core 2.0. Please follow the points below.

- If you are using **Visual Studio 2017 IDE** then you can create a new .NET Core project by selecting **New Project** from **File** menu.

- **Visual Studio Code** users can create a new project by running the below command on the project workspace terminal-

```
dotnet new console -o Application_Name
```

The **dotnet** command creates a new application of type **console** for you. The **-o** parameter creates a directory named **Application_Name** where your app is stored, and populates it with the required files.

- If you are using **Visual Studio 2017 IDE** then you have to install the package **log4net-loggly** into your project from **NuGet** by running the command on **Package Manager Console** as shown below-

```
Install-Package log4net-loggly
```

- If you are using **Visual Studio Code** then run the below command on the terminal to install the **log4net-loggly** package.

```
dotnet add package log4net-loggly
```

- Now when you create an applicaton in .NET Core, there is no App.config file exist already in the project so you have to create one.

  (a) For **Visual Studio 2017** users, you should right click on your project and create a **Application Configuration File** "App.config" on the root level of your project.

  (b) For **Visual Studio Code** users, you should simply create the same configuration file on the the folder structure where your another files exists.

- You should simply add the below configuration code in your App.config file to configure LogglyAppender in your application. Make sure the **configSections** block is the first element of the configuration in app.config. This is a requirement set by .NET.

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <configSections>
    <section name="log4net" type="log4net.Config.Log4NetConfigurationSectionHandler, log4net" />
  </configSections>
  <log4net>
    <root>
      <level value="ALL" />
      <appender-ref ref="LogglyAppender" />
    </root>
    <appender name="LogglyAppender" type="log4net.loggly.LogglyAppender, log4net-loggly">
      <rootUrl value="https://logs-01.loggly.com/" />
      <customerToken value="your-customer-token" />
      <tag value="your-custom-tags-separated-by-comma" />
      <logicalThreadContextKeys value="lkey1,lkey2" /> <!-- optional -->
      <globalContextKeys value="gkey1,gkey2" /> <!-- optional -->
    </appender>
  </log4net>
</configuration>
```

**Note: If you are using Visual Studio 2017 IDE then your application will not be able to read configurations from this App.config file until you do the following-**

- Right click on your **App.config** file from Solution Explorer, go to **Properties** and select the **Copy to Output Directory** to **Copy always**, click Apply and hit the OK button.

 If you are using **Visual Studio Code** then you don't need to do the extra settings for App.config file.

- As compare to .NET Frameworks, in .NET Core you don't need any AssemblyInfo.cs file to add the below code in-

```csharp
[assembly: log4net.Config.XmlConfigurator(Watch = true)]
```

The above code line allows the application to read the configuration from App.config file. In .NET Core applications, we will be reading the configuartions in a different way which is stated below-

- Add the following code inside the main method of your application file i.e. Program.cs-

```csharp
var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
log4net.Config.XmlConfigurator.Configure(logRepository, new FileInfo("App.config"));
```


Running the application in **Visual Studio 2017 IDE** is easy since you just need to click on the **Start** button to run your application.

If you are using **Visual Studio Code** then you have to run the below command on the terminal to run your .NET Core application-

```
dotnet run
```

And that's it. After doing this, you will see your .NET Core application logs flowing into Loggly.