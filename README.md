# **No Mans's Model Viewer** #

<div align="center"> <img src="https://i.imgur.com/hdBRZFL.png" width="400px"> </div>
<p></p>


<div align="center">
<img alt="GitHub tag (latest by date)" src="https://img.shields.io/github/v/tag/gregkwaste/NMSMV">
<a href="https://github.com/gregkwaste/NMSMV/releases"><img alt="GitHub release (latest by date)" src="https://img.shields.io/github/v/release/gregkwaste/NMSMV"></a>
<a href="https://github.com/gregkwaste/NMSMV/issues"><img alt="GitHub issues" src="https://img.shields.io/github/issues/gregkwaste/NMSMV"></a>
</div>


No Man's Model Viewer is an application that **was** primarity developed to preview No Man's Sky 3D assets. After years of tinkering with rendering methods, lighting systems, animation playback and more the viewer ended up supporting so much functionality that literally transformed it into a game engine. Therefore I took the decision to stop updating the app for a while, re-design it top-to-bottom and transform it in such a way that its primarily a game engine that can be used to preview NMS assets (and who knows what else in the future).

The engine - **Nibble Engine** - does not have to offer something more compared to other open/closed source engine projects out there. However, its a personal endeavor of mine to upgrade it continously and make it as good as I can make it. There is not a better way to understand complicated rendering mechanisms and data management without getting your hands dirty :D :D :D

Right now the engine is shipped together with the app that it used to preview NMS assets but as development continues the engine will have its separate repository. Functionality is not there yet to ship it independently but that is the goal for the future of the engine.



## **Repo Version** ##

* [Latest Version 0.89.4](https://github.com/gregkwaste/NMSMV/releases)
* [Wiki Page - OUTDATED](https://github.com/gregkwaste/NMSMV/wiki)

## **Features** ##
* Preview of .SCENE.MBIN files
* Support for Diffuse/Normal/Mask (roughness/metallic/ao) maps
* Support for animation playback on both skinned and static models, via parsing of the corresponding entity files
* Optimized renderer to support the rendering of very large scenes and increased framerates (well as much as .NET allows...)
* Basic implementation of a PBR shader pipeline that tries to emulate game shaders in an effort to preview assets as close to the game as possible (so close yet so far...)
* Procedural texture generation that tries to emulate NMS's texture missing process of procedural assets
* Procedural model generation (broken for a while, repair pending)
* Basic scenegraph editing (scenenode translation/rotation/scaling)
* Interface with libMbin to allow for a much more robust asset import and export directly to MBIN/EXML file format.
* Interface with libPSARC to allow for direct browsing of PAK contents including mods (No need to unpack game files to browse through the models)

## **WIP** ##
* 3D Gizmo implementation for a much more robust node manipulation.
* The project includes 2 more subprojects, a) a texture mixing app that provides a testbench of the procedural texture mixing process of NMS as implemented in the viewer and b) a texture viewer that interfaces with a custom DDS library to allow the preview of DX10 textures as well as multi-textures. I'm planning to better organize and maintain their code and eventually pack them together with the viewer in future releases.
* Assemble some wiki pages to explain the functionality and editing capabilities of the viewer.


### Build from Source? ###

TODO

### Contribution guidelines ###
* Please use the issue tracker to report any issues or any features/suggestions you may have.


## **Screenshots** ##
<div align="center"> <img src="https://i.imgur.com/9NX73V1h.png"></div>

## **Credits** ##
* monkeyman192 main maintainer of [libMBIN](https://github.com/monkeyman192/MBINCompiler)
* Fuzzy-Logik main maintainer of [libPSARC](https://github.com/Fuzzy-Logik/libPSARC)
* IanM32 for the amazing logo

## **Contact** ##
* Send me an email at gregkwaste@gmail.com
