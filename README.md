This is a repository for any mods I make for [R.E.P.O](https://store.steampowered.com/app/3241660/REPO/), which currently consists of a client-side UI mod that allows you to see what upgrades other players have, with the goal of making it easier to figure out how to distribute upgrades, and a "server-side" mod that redistributes/equalizes the health of all players when a level starts.

Requires [unity-mod-manager](github.com/newman55/unity-mod-manager) to load the mod. I've created a mod manager [config file](UnityModManagerConfig.REPO.xml) that seems to work with the DoorstopProxy method.

If you want to build the mods, you'll need to update the paths in [Base.props](Base.props) to point to wherever your game is installed.