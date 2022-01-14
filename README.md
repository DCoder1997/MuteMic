# MuteMic

Simple app to mute and unmute a microphone.
When the app starts, makes a tray icon with different colors:
- Dark gray: The selected microphone is not using or active
- Green: The microphone are using and is listening
- Red: The microphone are using and is muted.

To start the app change the config.cfg with a word of the name of 
your microphone, for example, for Xbox Kinect Microphone put xbox.

Is possible to start the app without a config.cfg file with arguments.
For example, for Xbox Kinect Microphone put xbox and the 
result of the start command is "C:\Folder\MuteMic.exe xbox".

When app is started, if you make a double click on tray icon, the app
mutes and unmute the microphone if is active (not dark gray icon). Is
possible to mute and unmute mic if make right click on the tray icon
and use the option named "Enable/Disable Mic".

The option on the context menu of the tray icon named "Notify - [State]"
is used to enable Windows 8 -> 10 notifications. When the state of the 
microphone changes, like mute and unmute, the app shows a notification with the
new state

This app uses NAudio library, see this library here: https://github.com/naudio/NAudio
