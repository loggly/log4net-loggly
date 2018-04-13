log4net-loggly
==============

Custom log4net appenders for importing logging events to loggly. It’s asynchronous and will send logs in the background without blocking your application. Check out Loggly's [.Net logging documentation](https://www.loggly.com/docs/net-logs/) to learn more.

<strong>Note:</strong> This library also has a support for .NET Core applications. Please see the section <strong>.NET Core Support</strong> below.

Download log4net-loggly package from NuGet. Use the following command.

    Install-Package log4net-loggly

Add the following code in your web.config to configure LogglyAppender in your application
```
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
        <inputKey value="your-customer-token" />
		<tag value="your-custom-tag" /> 
		<logicalThreadContextKeys value="lkey1,lkey2" /> <!-- optional -->
		<globalContextKeys value="gkey1,gkey2" /> <!-- optional -->
      </appender>
    </log4net>
```    
To send **GlobalContext** and **LogicalThreadContext** properties in your log you need define the list of used properties in the configuration. 

For GlobalContext Properties use 
```<globalContextKeys value="gkey1,gkey2" />```

For LogicalThreadContext Properties 
```<logicalThreadContextKeys value="lkey1,lkey2" />```


You can also use **layout** with in the Config to render logs according to your Pattern Layouts

     <layout type="log4net.Layout.PatternLayout">
         <conversionPattern value="%date [%thread] %-5level %logger %message" />
     </layout>

By default, library uses Loggly /bulk end point (https://www.loggly.com/docs/http-bulk-endpoint/). To use /inputs endpoint, add the following configuration in config file.

```
<logMode value="inputs" />
```


Add the following entry in your AssemblyInfo.cs
```
[assembly: log4net.Config.XmlConfigurator(Watch = true)]
```

Alternatively, you can add the following code in your Main method or in Global.asax file

```
log4net.Config.XmlConfigurator.Configure();
```

Create an object of the Log class using LogManager

    var logger = LogManager.GetLogger(typeof(Class));
    
Send logs to Loggly using the following code

```  
    logger.Info("log message");
```

<strong>For Console Application</strong>

You should add the following statement at the end of your Main method as the log4net-loggly library is asynchronous so there needs to be time for the threads the complete logging before the application exits.

```
Console.ReadKey();
```
<strong>.NET Core Support:</strong>

<strong>Prerequisites:</strong>

- Since this library support .NET Core target framework 2.0, make sure you are using version 15.3.0 or higher of Visual Studio IDE 2017. 

- You must have installed the .NET Core 2.0 SDK and Runtime environment.

- You may also have to install the .NET Core cross-platform development workload (in the Other Toolsets section). Please see the more details [here](https://docs.microsoft.com/en-us/dotnet/core/windows-prerequisites?tabs=netcore2x).

Once you are done with the environment setup, now you are all set to create your application in .NET Core target framework 2.0. Please follow the points below-

- You have to install the package <strong>log4net-loggly</strong> as shown below-

```
Install-Package log4net-loggly
```

- Now when you create an applicaton in .NET Core, there is no App.config file exist already in the project so you have to create one. Right click on your project and create a <strong>Application Configuration File</strong> "App.config" on the root level of your project.

- You should simply add the below configuration code in your App.config file to configure LogglyAppender in your application. Make sure the <strong>configSections</strong> block is the first element of the configuration in app.config. This is a requirement set by .NET.

```
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
        <inputKey value="your-customer-token" />
		<tag value="your-custom-tag" /> 
		<logicalThreadContextKeys value="lkey1,lkey2" /> <!-- optional -->
		<globalContextKeys value="gkey1,gkey2" /> <!-- optional -->
      </appender>
    </log4net>
```

<strong>Note: Your application will not be able to read configurations from this App.config file until you do the following-</strong>

- Right click on your <strong>App.config</strong> file from Solution Explorer, go to <strong>Properties</strong> and select the <strong>Copy to Output Directory</strong> to <strong>Copy always</strong>, click Apply and hit the OK button.

- As compare to .NET Frameworks, in .NET Core you don't need any AssemblyInfo.cs file to add the below code in-

```
[assembly: log4net.Config.XmlConfigurator(Watch = true)]
```

The above code line allows the application to read the configuration from App.config file. In .NET Core applications, we will be reading the configuartions in a different way which is stated below-

- Add the following code inside the main method of your application file i.e. Program.cs-

```
var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
log4net.Config.XmlConfigurator.Configure(logRepository, new FileInfo("App.config"));
```
After adding the above code you can simply create an object of the Log class using LogManager and start logging any plaintext, exceptions, or JSON events as shown below-

```
var logger = LogManager.GetLogger(typeof(Class));
//Send plaintext
logger.Info("your log message");

//Send an exception
logger.Error("your log message", new Exception("your exception message"));

//Send a JSON object
var items = new Dictionary<string,string>();
items.Add("key1","value1");
items.Add("key2", "value2");
logger.Info(items);
```

And that's it. After doing this, you will see your .NET Core application logs flowing into Loggly.


<strong>Added handling for LoggingEvent properties</strong>

Support for properties tied to a specific event and not a ThreadContext which is shared across the entire thread.

<strong>Added test cases project</strong> 

- Added unit test cases project in library to test consistency for new feature.

- User can select test cases project in Visual Studio and can simply run all test cases from Test Explorer.

