# CS-4361-Project---Edgevision

A first-person forced-perspective puzzle game built in Unity. Pick up an object, aim at a surface, and the object moves there and resizes so it looks the same size on screen, like in *Superliminal*.

## Teammate setup

### 1. Install the tools
- **Unity Hub**, then install **Unity 6000.6.7f1** from it (Installs → Install Editor → Archive if that exact version isn't listed). Use exactly this version; a different one makes Unity try to upgrade the project and creates noisy changes.
- **Git** and **Git LFS**. After installing Git LFS, run once:
  ```bash
  git lfs install
  ```

### 2. Clone the repo
```bash
git clone https://github.com/HomasN/CS-4361-Project---Edgevision.git
```
You need to be added as a collaborator on the repo first.

### 3. Open the project in Unity
- In Unity Hub: **Add → Add project from disk**, then select the **`Edgevision` folder inside the clone** (the one containing `Assets`, `Packages` and `ProjectSettings`), not the top-level folder.
- The first open takes a few minutes while Unity builds its `Library` cache and downloads packages. `Library` is ignored by Git, so everyone builds their own.

### 4. Open the test room
- Open `Assets/Scenes/PrototypeRoom.unity` and press **Play**.
- If the scene looks wrong or is missing, run **Edgevision > Build Prototype Room** from the menu bar to rebuild it from the script.

### Controls
| Input | Action |
| --- | --- |
| WASD | Move |
| Mouse | Look |
| Space | Jump |
| Left click | Pick up / let go of an object |
| Esc | Free the mouse cursor (click to re-lock) |

## Day-to-day rules
- Run `git pull` before opening Unity, and save in Unity before committing.
- Don't edit the same scene at the same time as someone else. Unity scene files merge badly, so agree on who is working in `PrototypeRoom.unity`, or work in your own copy of the scene.
- Work on your own branch and merge to `main` through a pull request.
- Always commit the `.meta` file that sits next to each asset. Unity needs them to keep references between files intact.
