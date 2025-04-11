## About

This is a repository for any mods I make for [R.E.P.O](https://store.steampowered.com/app/3241660/REPO/), which currently consists of two different mods. The mods require [unity-mod-manager](github.com/newman55/unity-mod-manager) to load. I've created a mod manager [config file](UnityModManagerConfig.REPO.xml) that seems to work with the DoorstopProxy method. 

You can download the latest release [here](https://github.com/ElectroJr/RepoMods/releases/latest). If you want to build the mods yourself, you'll need to update the paths in [Base.props](Base.props) to point to wherever your game is installed.

## ShowUpgrades

This is a client-side UI mod that allows you to see what upgrades other players have, with the goal of making it easier to figure out how to distribute upgrades. The upgrades are colour-coded, with your own upgrades always listed last in the default colour (white). Its not ideal, especially when multiple players use the same colour or someone choses a very dark colour, but its the easiest way I could think of to present the information concisely.

<img src="https://github.com/user-attachments/assets/d40cc6be-12c9-42a5-898d-209336e43430" width="200">

## HealthEqualizer

This mod redistributes / equalizes the health of all players when a level starts, to avoid the tedium of having to figure out who needs to receive or donate health after going shopping. This is a "server-side" mod that only the host needs to have installed.
It has not yet been thorroughly tested, but seems to work?
