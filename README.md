# Grimoire

Grimoire is a general purpose bot for discord. It includes moderation tools, advanced logging features, and a leveling
system. This is the successor to my previous bots Fuzzy, Lumberjack, and Anubis and includes all the features of those
bots as well as new functionality.

## FAQ

Q: **I was using one of your previous bots. Do I have to setup everything from scratch again?**
A: No! All of your data and settings have been transered over to the new bot it just needs to be invited and it will
work straight away.

Q: **I was only using one of your bots. Do I now have to use all the new features that I was using before?**
A: No! Grimoire has been split into modules that you can enable or disable with "/modules set" command and see the
currently enabled modules with "/modules view".

Q: **I don't want my messages being stored on Grimoire.**
A: Grimoire will only store your messages if the "Message Log" module is enabled.

Q: **How long is message data stored for?**
A: Grimoire will purge all message data 30 days after the original message was sent.

Q: **How do I invite Grimoire to my server?**
A: You can find an invite
link [here.](https://discord.com/api/oauth2/authorize?client_id=885624963866959963&permissions=1512197975231&scope=applications.commands%20bot)

Q: **Can I run my own copy of Grimoire?**
A: You sure can. The requirements to run are listed in the [Requirements Section](#Requirements)

# Requirements

Grimoire runs using [.NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) and
a [PostgreSQL database](https://www.postgresql.org/download/). Optionally you can run Grimoire in
a [docker container](https://www.docker.com/get-started/).

You will also need a [Discord Bot Account and Token](https://discord.com/developers/docs/getting-started)

# Configuration

There is a few parameters that need to be set for Grimoire to work. Under Grimoire folder there is an `appsettings.json`
file where you can set the configuration. The following properties are available to be set.

* `token` The Discord Bot token to connect with.
* `ConnectionStrings.Grimoire` The connection string for the database in
  the [Npgsql format](https://www.npgsql.org/doc/connection-string-parameters.html)
* `ConnectionProperties.\*` This section will build a connection string if one is not provided in
  `ConnectionStrings.Grimoire`
* `ConnectionProperties.Hostname` The hostname of the database.
* `ConnectionProperties.Port` The port the database uses to connect.
* `ConnectionProperties.DbName` The name of the database.
* `ConnectionProperties.Username` The Username to connect to the database with.
* `ConnectionProperties.Password` The password for the database user.
* `validImageExtensions` A comma-separated list of image file extensions. Required: image embeds fail without it.
* `channelId` *(Optional)* The channel that Grimoire posts urgent alerts (errors) to.
* `warningChannelId` *(Optional)* The channel for warning alerts and the daily warning report.
* `warningReportTimeUtc` *(Optional)* The time of day (UTC) the daily warning report is posted.
* `pluralkitToken`, `pluralkitUserAgent`, `pluralkitApiEndpoint` *(Optional)* PluralKit lookups, used to log proxied
  messages under the member behind them. Lookups are off unless the token and user agent are set.
* `Serilog.\*` Sets the configuration for logs. Reasonable defaults have been set but you can customize them here.

You can also set these properties using environment variables, which override the settings in the
`appsettings.json` file. Keep secrets such as the token and the database password in environment variables rather
than in the file. Use `__` (double underscore) as a seperator between levels. If you plan on running Grimoire in
a docker container, you can set the configuration in the `docker-compose.yml` file.

# Running

## Docker

* Clone this repository: `git clone https://github.com/Netharria/Grimoire`
* Set the [configuration](#configuration) in the `docker_compose.yml`
* Run the bot: `docker-compose up -d`

```
$ git clone https://github.com/Netharria/Grimoire
$ nano docker-compose.yml
$ docker-compose up -d
```

A database admin console has been included which can be reached at `http://localhost:8080` to connect to the database
enter server location `host.docker.internal:5432` and use the credentials in the docker-compose.yml.

## Manually

* Install the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
* Clone this repository: `git clone https://github.com/Netharria/Grimoire`
* Set the [configuration](#configuration) in the `appsettings.json`
* Change directory to Grimoire
* Run the bot: dotnet run

```
$ git clone https://github.com/Netharria/Grimoire
$ cd Grimoire
$ nano appsettings.json
$ dotnet run
```
