# MetaSprite

MetaSprite is an Unity plugin that lets you import [Aseprite][aseprite]'s .ase file into Unity, as Mecanim animation clips/controllers. It also has rich **metadata support** built in, allowing one to manipulate colliders, change transforms, send messages (and much more!) easily in Aseprite.

MetaSprite is currently in early developement stage.

# Feature overview

* Doesn't require external aseprite executable to run
* Efficient atlas packing algorithm
* Simple workflow, only requiring right-clicking on the sprite file and choose Import
* Extensive metadata support
  * Commented (ignored) Layers/Tags
  * Manipulate colliders/events/positions/sprite pivots using image data
  * Specify clip looping using frame tag properties
  * ...

# Credits

* [tommo](https://github.com/tommo)'s Aseprite importer in gii engine
* [talecrafter](https://github.com/talecrafter)'s [AnimationImporter](https://github.com/talecrafter/AnimationImporter), where the code of this project is initially based from

[aseprite]: https://aseprite.org
