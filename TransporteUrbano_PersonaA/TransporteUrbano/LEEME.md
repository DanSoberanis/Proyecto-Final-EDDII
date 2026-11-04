# Red de transporte urbano: parte de la Persona A

Proyecto de laboratorio 2, Estructura de Datos II (C#, Windows Forms, .NET 8).

## Cómo abrirlo
1. Instala el SDK de .NET 8 (o Visual Studio 2022 con la carga "Desarrollo de escritorio con .NET").
2. Abre `TransporteUrbano.csproj` y ejecuta (F5), o desde la carpeta: `dotnet run`.
3. Al abrir carga `Datos/paradas.csv` y `Datos/rutas.csv`. Con el grafo pendiente verás "Grafo pendiente (Persona B)" y todo lo demás funciona.

## Quién hizo qué
| Archivo | Responsable |
|---|---|
| Estructuras/ListaEnlazada.cs, TablaHash.cs, Trie.cs, UnionFind.cs | Persona A |
| Servicios/CargadorArchivos.cs, Servicios/SistemaTransporte.cs (integración) | Persona A (la Persona B revisa y amplía la parte del grafo) |
| UI/*, Estructuras/MapaBase.cs | Persona A |
| Estructuras/Grafo, Dijkstra, MinHeap (por crear) | Persona B |

## Para la Persona B (cómo conectar el Grafo)
1. Crea tu clase `Grafo` en `Estructuras/` (namespace `TransporteUrbano.Estructuras`) que implemente `IGrafoTransporte`
   (lista de adyacencia, BFS con `Queue<T>`, DFS, Dijkstra con `MinHeap` propio, `Disponible => true`).
2. En `Estructuras/FabricaGrafo.cs` cambia `return new GrafoNoDisponible();` por `return new Grafo();`.
3. No necesitas tocar la interfaz gráfica: el botón Calcular, los recorridos BFS/DFS y el mapa ya llaman a tu grafo.
4. Dijkstra solo debe llenar `Exito`, `Mensaje`, `Camino` (origen y destino incluidos) y `PesoTotal`.
   Los totales de tiempo y costo los calcula `SistemaTransporte`.

## Flujo integrado
Trie (sugiere) -> Tabla Hash (registro por código) -> Union-Find (misma zona) -> Grafo (Dijkstra).
La pestaña "Calcular" muestra qué estructura participó en cada paso.

## Mapa
Sin internet ni librerías: se dibuja con GDI+ usando latitud y longitud, sobre el contorno de Guatemala
incrustado en `MapaBase.cs`. Rueda del ratón = zoom, arrastrar = mover, botón del globo = ver todo el país.

## Formato de los archivos
paradas: `codigo,nombre,latitud,longitud`
rutas: `origen,destino,tiempo,costo,unidireccional` (1 = un solo sentido)

## Mapa real (GMap.NET) y mapa ligero
- Por defecto se abre el **mapa real** de OpenStreetMap (paquete NuGet `GMap.NET.WinForms`). Necesita internet para
  descargar el fondo; lo que ya se vio queda guardado en disco (caché) y se puede volver a ver sin conexión.
- El botón **Mapa ligero / Mapa real** de la barra superior cambia entre los dos al instante.
- El **mapa ligero** (`MapaControl.cs`) no usa internet ni librerías. Si el mapa real falla al iniciar, el programa
  usa este automáticamente. También se puede abrir directo con: `dotnet run -- --ligero`
- Antes de la defensa: abrir el programa con internet, acercar y alejar sobre Xela (y la zona que se vaya a mostrar)
  para llenar la caché, y probar una vez sin internet.
- Archivos del mapa: `UI/IMapaRed.cs`, `UI/FabricaMapa.cs`, `UI/MapaGMapControl.cs`, `UI/MapaControl.cs`.
