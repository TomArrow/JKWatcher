# Configs

Place .ini file with the desired config name in "configs" folder next to .exe.

In the GUI, select the desired config and hit "Execute".

## .ini format

### ```[__general__]``` section

General settings for this instance of JKWatcher. Keys, meaning and possible values.

#### ctfAutoConnect

0 or 1. Whether CTF auto connecter should be enabled. Connects to CTF games and spawns a CTF watcher (2 connections).

#### ctfAutoConnectWithStrobe

0 or 1. Whether CTF auto connecter should connect a third client with a Strobe camera operator (flips through players very fast and can help inform the CTF watcher about good decisions).

#### ffaAutoConnect

0 or 1. Whether FFA auto connecter should be enabled.

#### ffaAutoConnectSilent

0 or 1. Whether FFA auto connecter should connect in silent mode. That means: No bot commands, memes etc. And no name colors for demo timestamp.

#### ffaAutoConnectExclude

Comma-separated list of search strings for servers to exclude from ffa auto connect.

#### ffaAutoConnectKickable

0 or 1. When auto-connecting to FFA, allow ourselves to be kicked.

#### ffaAutoConnectKickReconnectDelay

Number of minutes to wait before FFA auto-connecting to a server again after being kicked

#### autoJoinCheckInterval

Interval (in minutes) in which to check for new servers for auto join/delayed connecter.

#### jkaMode 

0 or 1. Jedi Academy mode instead of JK2.

#### mohMode 

0 or 1. MOH mode instead of JK2.

#### allJK2Versions

0 or 1. For JK2, query servers of all JK2 versions, not just 1.02.

### Server sections

Individual server sections require one section per server. The name of the section is irrelevant but avoid duplicates. And they can't be named ```[__general__]``` for obvious reasons.

List of possible keys with meaning and possible values:

#### ip

Specific IP to identify this server in normal ```XXX.XXX.XXX.XXX:XXXXX``` format. You **always** need either this or *hostName*.

#### hostName

Search string for the server name to identify this server. You **always** need either this or *ip*. This does not need to be the full server name, it can be a part of the server name.

#### playerName

Player name(s) to use when connecting to this server. If multiple names, separate by ```\```, as it can't be part of a player name anyway.

#### password

If required, password to connect to the server.

#### autoRecord

1 or 0. Automatically start recording demos after connecting.

#### botSnaps

Snaps value to use if all active players are bots. (discards other packets)

#### pingAdjust

Adjusts your visible ping value. Potentially unstable, especially with unstable internet, use at your own risk. Can be positive/negative values.

#### attachClientNumToName

0 or 1. Whether to force attach client number to playername.

#### demoTimeColorNames

0 or 1. Whether to use demo start timestamp colors for the playername. Encodes time of when the demo recording started into the playername as colors.

#### silentMode

0 or 1. Connects silently, aka no bot commands.

#### gameTypes

Comma-separated list of gametypes. Will only connect if gametype matches one of the provided ones. Options: ffa, holocron, jedimaster, duel, powerduel, sp, tffa, siege, cty, ctf, 1flagctf, obelisk, harvester, teamrounds, objective, tow, liberation (not all are supported by each game ofc)

#### pollingInterval

Only works if the server is identified via ```ip```. How often to query new server info? Milliseconds.

#### mohProtocol

Set to 1 if this server requires the MOH protocol. Mainly required in combination with pollingInterval if polling MOH servers, as their connectionless packets (used for server info/status) work differently.

#### maxTimeSinceMapChange

Maximum time in milliseconds since last detected map change. Mostly makes sense in combination with pollingInterval. If last detected map change happened more than the set amount of milliseconds ago, don't connect.

#### inactive

Set to 1 if this connection should be not active by default.

#### minPlayers

Minimum amount of real players (non-bots) on server required to connect.

#### timeFromDisconnect

Minimum amount of time that must have passed since the last time we disconnected from this server, in minutes. Optionally can be a range like "5-10". A random value will be picked in between.

#### retries

Number of retries of connecting initially after executing the script. It's a little bit irrelevant now since any failed connects are added to a delayed connect list anyway but might take a bit longer that way.

#### delayed

0 or 1. Whether this should go to the "delayed connect" list. Kind of irrelevant now, see ```retries```

#### chance

0-100 (default 100). Whenever the other criteria all match, roll the dice whether we should actually connect. Number is the probability in percent of actually connecting.

#### dailyChance

0-100 (default 100). Whenever the other criteria all match, roll the dice for each day whether on that day we should connect. This random chance is controlled with the current date as seed.

#### watchers

List of watchers to spawn, comma-separated. Options: ocd,ctf,strobe,ffa
Optionally, you can attach extra options to watchers, example:
 
```ctf{priorityPlayer:playername;otheroption:optionvalue2},strobe{otheroption2:optionvalue3}```

Currently available options are:

- ctf: priorityPlayer: Player search string. In case one of the 2 ctf connections fails, the remaining connection will be used to follow the team that this player is on.

#### delayPerWatcher

If adding multiple watchers, how long to wait between adding each? Milliseconds.

#### disconnectTriggers

Comma-separated: Triggers for disconnecting from the server. 

Possible options: 

- ```gameTypeNotCTF```: Disconnects when the gametype is not CTF or CTY.
- ```gametype_not:ctf;cty:60000```: Disconnects when the gametype is not CTF or CTY for more than 1 minute (replace with numbers of your choice. game types can be - separated by semicolon: ffa, holocron, jedimaster, duel, powerduel, sp, tffa, siege, cty, ctf, 1flagctf, obelisk, harvester, teamrounds, objective, tow, liberation)
- ```kicked```: Disconnects when we were kicked (not exhaustively supported yet).
- ```playercount_under:8-10:60000```: Disconnects if player count falls under 8 to 10 for more than 1 minute (replace with numbers of your choice. player count can be range or single number without "-")
- ```connectedtime_over:55-60```: Disconnects if we were connected for more than 55 to 60 minutes (random within that range, change numbers to what you desire or use single number without "-")
- ```mapchange```: Disconnect if a map change is detected.

#### mapChangeCommands 

Commands to send to the server when a map change is detected. A special command here is ```wait``` which isn't sent to the server but instead delays following commands by the amount of milliseconds specified, e.g. "wait 5000" for a 5-second delay. Separate commands via semicolon ```;```

#### quickCommands

Commands that are shown in the GUI to allow you to quickly and comfortably send them with a single click. Separate via semicolon ```;```. ```wait``` can be used same as in mapChangeCommands.

#### conditionalCommands

Special conditional commands that are set when specific conditions are met. 

Syntax:
```condition_type:condition:command;command2;command3,condition_type2:condition:command4;command5```

First the condition type (see below), then some kind of condition for that condition type (see below), then commands to execute, separated by semicolon ```;```. ```wait``` can be used same as in mapChangeCommands. Multiple conditional statements like this can be used, separated by comma ```,```.

Some condition types allow you to use placeholders in the commands (see below).

Condition types:

- ```chat_contains```: Matches chat contents. Condition is a regular expression (C# Regex object) - if it matches the chat string, the commands are executed. Usable placeholders in commands are ```$name``` (name of the player who sent the matching chat) and ```$clientnum``` (his client number).
- ```playeractive_matchname```: Matches playernames of players who become active (join a non-spectator team). Condition is a regular expression (C# Regex object) - if it matches the player name, the commands are executed. Usable placeholders in commands are ```$name``` (name of the player whose name matched) and ```$clientnum``` (his client number).
- ```print_contains_```: Matches print outputs sent by the server. Condition is a regular expression (C# Regex object) - if it matches the printed string, the commands are executed. No placeholders available.

#### demoMeta

Additional meta data to always write into demos for this server.

Syntax:
```key1:value1,key2:value2,blahkey:blahvalue```

#### sunsConnectSubscribe

This lets you subscribe to a SUNS UDP notification server. If a notification is received, you are force connected to this server.

Example:

```sunsConnectSubscribe=127.0.0.1:52476;subscriptionKey```

You can set specific byte values in the subscription key by writing them as for example \x1 = byte value of 1 or \xff = byte value of 255