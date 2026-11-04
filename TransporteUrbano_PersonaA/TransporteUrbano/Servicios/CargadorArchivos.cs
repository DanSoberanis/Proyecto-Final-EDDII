using System;
using System.Globalization;
using System.IO;
using System.Text;
using TransporteUrbano.Estructuras;
using TransporteUrbano.Models;

namespace TransporteUrbano.Servicios
{
    /// <summary>Resumen de una carga de archivo, para mostrarlo en pantalla.</summary>
    public class ResultadoCarga
    {
        public bool ArchivoValido;
        public int Leidos;      // filas con formato correcto
        public int Cargados;    // filas que llegaron a registrarse en el sistema
        public ListaEnlazada<string> Errores = new ListaEnlazada<string>();
    }

    /// <summary>
    /// [PERSONA A] Lectura y escritura de archivos .csv / .txt.
    /// Verifica que el archivo exista y tenga extensión válida, detecta el separador (coma, punto y coma o tab),
    /// ignora el encabezado, las líneas vacías y las que empiezan con '#', y reporta cada línea con error
    /// sin detener la carga.
    ///
    /// Formatos:
    ///   paradas: codigo,nombre,latitud,longitud
    ///   rutas:   origen,destino,tiempo,costo[,unidireccional]   (unidireccional: 1/0, si/no, true/false)
    /// </summary>
    public static class CargadorArchivos
    {
        private static bool ValidarArchivo(string ruta, ListaEnlazada<string> errores)
        {
            if (string.IsNullOrWhiteSpace(ruta)) { errores.AgregarFinal("No se indicó la ruta del archivo."); return false; }
            if (!File.Exists(ruta)) { errores.AgregarFinal("El archivo no existe: " + ruta); return false; }
            string ext = Path.GetExtension(ruta).ToLowerInvariant();
            if (ext != ".csv" && ext != ".txt")
            {
                errores.AgregarFinal("Formato no soportado (" + ext + "). Usa un archivo .csv o .txt.");
                return false;
            }
            return true;
        }

        private static char Separador(string linea)
        {
            if (linea.IndexOf(';') >= 0) return ';';
            if (linea.IndexOf('\t') >= 0) return '\t';
            return ',';
        }

        private static bool TryDouble(string texto, char separador, out double valor)
        {
            string t = texto.Trim();
            if (separador == ';') t = t.Replace(',', '.');   // en archivos con ';' el decimal puede venir con coma
            return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out valor);
        }

        private static bool TryBool(string texto, out bool valor)
        {
            switch (texto.Trim().ToLowerInvariant())
            {
                case "1": case "true": case "si": case "sí": case "s": case "x":
                    valor = true; return true;
                case "0": case "false": case "no": case "n": case "":
                    valor = false; return true;
            }
            valor = false;
            return false;
        }

        public static bool LeerParadas(string ruta, ListaEnlazada<Parada> salida, ListaEnlazada<string> errores)
        {
            if (!ValidarArchivo(ruta, errores)) return false;
            try
            {
                int numero = 0;
                bool primeraFila = true;
                foreach (string cruda in File.ReadLines(ruta))
                {
                    numero++;
                    string linea = cruda.Trim();
                    if (linea.Length == 0 || linea[0] == '#') continue;

                    char sep = Separador(linea);
                    string[] campos = linea.Split(sep);
                    double lat = 0, lon = 0;

                    if (campos.Length < 4)
                    {
                        errores.AgregarFinal("Línea " + numero + ": se esperaban 4 columnas (codigo,nombre,latitud,longitud).");
                    }
                    else if (!TryDouble(campos[2], sep, out lat) || !TryDouble(campos[3], sep, out lon))
                    {
                        if (!primeraFila)   // la primera fila no numérica es el encabezado
                            errores.AgregarFinal("Línea " + numero + ": latitud o longitud no son números.");
                    }
                    else
                    {
                        salida.AgregarFinal(new Parada(campos[0].Trim(), campos[1].Trim(), lat, lon));
                    }
                    primeraFila = false;
                }
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                errores.AgregarFinal("No se pudo leer el archivo: " + ex.Message);
                return false;
            }
        }

        public static bool LeerRutas(string ruta, ListaEnlazada<Ruta> salida, ListaEnlazada<string> errores)
        {
            if (!ValidarArchivo(ruta, errores)) return false;
            try
            {
                int numero = 0;
                bool primeraFila = true;
                foreach (string cruda in File.ReadLines(ruta))
                {
                    numero++;
                    string linea = cruda.Trim();
                    if (linea.Length == 0 || linea[0] == '#') continue;

                    char sep = Separador(linea);
                    string[] campos = linea.Split(sep);
                    double tiempo = 0, costo = 0;
                    bool uni = false;

                    if (campos.Length < 4)
                    {
                        errores.AgregarFinal("Línea " + numero + ": se esperaban al menos 4 columnas (origen,destino,tiempo,costo).");
                    }
                    else if (!TryDouble(campos[2], sep, out tiempo) || !TryDouble(campos[3], sep, out costo))
                    {
                        if (!primeraFila)
                            errores.AgregarFinal("Línea " + numero + ": tiempo o costo no son números.");
                    }
                    else if (campos.Length >= 5 && !TryBool(campos[4], out uni))
                    {
                        errores.AgregarFinal("Línea " + numero + ": el valor de 'unidireccional' no se entiende (" + campos[4].Trim() + ").");
                    }
                    else
                    {
                        salida.AgregarFinal(new Ruta(campos[0].Trim(), campos[1].Trim(), tiempo, costo, uni));
                    }
                    primeraFila = false;
                }
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                errores.AgregarFinal("No se pudo leer el archivo: " + ex.Message);
                return false;
            }
        }

        private static string Num(double d) { return d.ToString("0.######", CultureInfo.InvariantCulture); }

        public static void GuardarParadas(string ruta, ListaEnlazada<Parada> paradas)
        {
            using (var w = new StreamWriter(ruta, false, new UTF8Encoding(false)))
            {
                w.WriteLine("codigo,nombre,latitud,longitud");
                foreach (Parada p in paradas)
                    w.WriteLine(p.Codigo + "," + p.Nombre + "," + Num(p.Latitud) + "," + Num(p.Longitud));
            }
        }

        public static void GuardarRutas(string ruta, ListaEnlazada<Ruta> rutas)
        {
            using (var w = new StreamWriter(ruta, false, new UTF8Encoding(false)))
            {
                w.WriteLine("origen,destino,tiempo,costo,unidireccional");
                foreach (Ruta r in rutas)
                    w.WriteLine(r.Origen + "," + r.Destino + "," + Num(r.Tiempo) + "," + Num(r.Costo) + "," + (r.Unidireccional ? "1" : "0"));
            }
        }
    }
}
