# Custom-NavMesh-Agent-Script-For-Unity-
I got tired of my standard Nav Mesh Agents steering around each other like cars so I made my own custom solution with custom obstacle avoidance. 

https://github.com/user-attachments/assets/d71c81c4-cea2-4eb3-9d27-705c7b1fb51d

## Features:
  - Simple layer-based obstacle avoidance (NPC's turn around each other)
  - Custom movement with Character Controller
  - Path regeneration when stuck, not facing direction to path corner, or far away from path, or if the NPC hasn't moved in a while
  - Repository includes an example NPCBehavior script utilizing the NPCAgent script with an NPC state machine

## Usage:
This script requires a Character Controller to function. You also need a baked NavMesh Surface in your scene to use it. 

## How it works:
Basicaly this script utilizes NavMesh.CalculatePath to calculate A* paths on a baked NavMesh Surface, then injects its own custom movement logic on that using a Character Controller component. Obstacle avoidance uses a CapsuleCast in front of the NPC that is the same radius and height of the NPC's character controller. If that CapsuleCast fails, which happens if an obstacle is too close to the NPC, then a fallback Raycast is used to catch it. Then the NPC does two CheckBox checks to ensure which side is free to turn into, that way it doesn't accidentally turn into a wall or something. If two NPC's are head on, they turn the opposite direction that way they don't accidentally turn into each other. The NPCs know what objects to turn against if they are on the obstacle layer. 

You may be asking why I created this when a standard NavMesh Agent would do just fine, but honestly in my opinion NavMesh Agent's aren't built for realistic crowd simulation. When I was trying to create a city block with NPCs using NavMesh Agent's, they kept steering around each other like cars. I understand that is helpful for some people, but it just wasn't helpful for my use case. I wanted NPC's to turn around each other realistically like in Postal 2 instead of steering around each other. I tried tweaking all the settings I could in the NavMesh Agent but nothing was working, so I created it myself. The script itself is a little buggy and the path regeneration does fail sometimes, but other than that it works pretty good. 
