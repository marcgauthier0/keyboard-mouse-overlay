# Guide de capture OBS pour Gaming Keypress Overlay

## 🎯 Problème : Fenêtre noire dans OBS avec "Window Capture"

Quand vous utilisez `AllowsTransparency="True"` dans WPF, la fenêtre ne peut **pas** être capturée avec "Window Capture" dans OBS car Windows bloque la capture des fenêtres transparentes.

---

## ✅ Solutions pour capturer l'overlay dans OBS

### Solution 1 : Utiliser "Display Capture" (Recommandé)

1. Dans OBS, ajoutez une source **"Display Capture"**
2. Sélectionnez votre écran principal
3. Ajoutez un **filtre "Crop/Pad"** pour découper seulement la zone de l'overlay
4. Positionnez et redimensionnez la source dans OBS

**Avantages :**
- ✅ Fonctionne parfaitement avec les fenêtres transparentes
- ✅ Pas besoin de modifier l'application
- ✅ Meilleure qualité

---

### Solution 2 : Utiliser "Game Capture"

1. Dans OBS, ajoutez une source **"Game Capture"**
2. Sélectionnez "Capture any fullscreen application"
3. L'overlay devrait apparaître automatiquement si l'application est au premier plan

**Avantages :**
- ✅ Bonne performance
- ✅ Fonctionne avec les fenêtres transparentes

---

### Solution 3 : Utiliser "Browser Source" avec HTML (Avancé)

1. Créez une page HTML qui affiche les touches
2. Dans OBS, ajoutez une source **"Browser Source"**
3. Pointez vers le fichier HTML local ou une URL

**Avantages :**
- ✅ Contrôle total sur le design
- ✅ Fonctionne dans tous les cas

---

## ⚠️ Pourquoi "Window Capture" ne fonctionne pas ?

`AllowsTransparency="True"` utilise `WS_EX_LAYERED` de Windows, qui empêche la capture de fenêtre via les API standard. C'est une limitation de Windows, pas de WPF ou d'OBS.

---

## 💡 Astuce Pro

Pour une meilleure capture dans OBS :
- Utilisez un fond **presque transparent** au lieu de **complètement transparent**
- Cela permet parfois à OBS de mieux détecter la fenêtre
- Exemple : `#01000000` au lieu de `Transparent`

---

## 🔧 Alternative : Modifier le code pour OBS

Si vous voulez absolument utiliser "Window Capture", vous pouvez :

1. **Modifier `MainWindow.xaml`** : Changer `AllowsTransparency="True"` en `AllowsTransparency="False"`
2. **Utiliser un fond semi-transparent** : `Background="#E6000000"` au lieu de `Transparent`
3. **Redémarrer l'application**

**Attention :** Cela désactivera la transparence complète, mais la fenêtre sera capturable dans OBS.
