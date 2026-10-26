using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Porter2Stemmer;

// Retin inf imp despre fiecare articol
class Document
{
    public string Id = "";
    public string Folder = "";
    public List<string> Topics = new List<string>();
    public Dictionary<string, int> Counts = new Dictionary<string, int>();
}

class Program
{
    static void Main()
    {
        // Stabilesc caile catre fisiere si foldere
        string bazaReuters =
            @"C:\Users\Karla\Desktop\ReutersDataSet\Reuters";

        string folderDate =
            Path.Combine(bazaReuters, "Reuters_34");

        string caleStopwords =
            Path.Combine(bazaReuters, "stopwords.txt");

        string caleDictionar =
            Path.Combine(bazaReuters, "Dictionar.txt");

        string caleRezultate =
            Path.Combine(bazaReuters, "Rezultate.txt");

        string folderArticole =
            Path.Combine(bazaReuters, "Articole_curatate");

        if (!Directory.Exists(folderDate))
        {
            Console.WriteLine("Folderul nu exista: " + folderDate);
            Console.ReadKey();
            return;
        }

        if (!File.Exists(caleStopwords))
        {
            Console.WriteLine("Nu exista fisierul: " + caleStopwords);
            Console.WriteLine("Adauga stopwords.txt in folderul Reuters.");
            Console.ReadKey();
            return;
        }

        // Citesc lista de stopwords
        HashSet<string> stopwords = new HashSet<string>(
            File.ReadAllLines(caleStopwords)
                .Select(w => w.Trim().ToLowerInvariant())
                .Where(w => w.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        EnglishPorter2Stemmer stemmer = new EnglishPorter2Stemmer();

        Directory.CreateDirectory(folderArticole);

        List<Document> documente = new List<Document>();
        SortedSet<string> dictionar =
            new SortedSet<string>(StringComparer.Ordinal);

        // Parcurg articolele din Training si Testing
        foreach (string numeFolder in new[] { "Training", "Testing" })
        {
            string caleFolder = Path.Combine(folderDate, numeFolder);

            if (!Directory.Exists(caleFolder))
            {
                Console.WriteLine("Folder negasit: " + caleFolder);
                continue;
            }

            string folderArticoleSet =
                Path.Combine(folderArticole, numeFolder);

            Directory.CreateDirectory(folderArticoleSet);

            string[] fisiere = Directory.GetFiles(
                caleFolder, "*.xml", SearchOption.AllDirectories);

            Array.Sort(fisiere, StringComparer.OrdinalIgnoreCase);

            foreach (string fisier in fisiere)
            {
                try
                {
                    Document doc = Citeste(
                        fisier,
                        numeFolder,
                        stopwords,
                        stemmer,
                        folderArticoleSet);

                    documente.Add(doc);

                    foreach (string cuvant in doc.Counts.Keys)
                    {
                        dictionar.Add(cuvant);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        "Eroare la " + fisier + ": " + ex.Message);
                }
            }
        }

        // Atribui fiecarui cuvant un index in dictionar
        Dictionary<string, int> pozitii =
            new Dictionary<string, int>();

        int index = 0;

        foreach (string cuvant in dictionar)
        {
            pozitii[cuvant] = index;
            index++;
        }

        // Salvez dictionarul cu indexul fiecarui cuvant
        using (StreamWriter sw = new StreamWriter(
            caleDictionar, false, new UTF8Encoding(true)))
        {
            foreach (string cuvant in dictionar)
            {
                sw.WriteLine(pozitii[cuvant] + ": " + cuvant);
            }
        }

        // Scriu atributele si reprezentarea articolelor prin vectori rari
        using (StreamWriter sw = new StreamWriter(
            caleRezultate, false, new UTF8Encoding(true)))
        {
            foreach (string cuvant in dictionar)
            {
                sw.WriteLine("@attribute " + cuvant);
            }

            sw.WriteLine();
            sw.WriteLine("@data");

            foreach (Document d in documente)
            {
                var vectorRar = d.Counts
                    .Select(kv => new
                    {
                        Pozitie = pozitii[kv.Key],
                        Frecventa = kv.Value
                    })
                    .OrderBy(x => x.Pozitie)
                    .Select(x => x.Pozitie + ":" + x.Frecventa);

                sw.WriteLine(
                    d.Id + " # " +
                    string.Join(" ", vectorRar) + " # " +
                    string.Join(" ", d.Topics) + " # " +
                    d.Folder);
            }
        }

        Console.WriteLine("Procesarea s-a terminat!");
        Console.WriteLine("Documente procesate: " + documente.Count);
        Console.WriteLine("Radacini distincte: " + dictionar.Count);
        Console.WriteLine("Dictionar: " + caleDictionar);
        Console.WriteLine("Rezultate: " + caleRezultate);
        Console.WriteLine("Articole curate: " + folderArticole);

        Console.ReadKey();
    }

    // Citesc un articol, il preprocesez si numar cuvintele
    static Document Citeste(
        string fisier,
        string folder,
        HashSet<string> stopwords,
        EnglishPorter2Stemmer stemmer,
        string folderArticole)
    {
        XDocument xml = XDocument.Load(fisier);

        Document rezultat = new Document
        {
            Id = Path.GetFileNameWithoutExtension(fisier),
            Folder = folder
        };

        string titlu = string.Join(" ",
            xml.Descendants()
                .Where(x => x.Name.LocalName == "title")
                .Select(x => x.Value));

        string textArticol = string.Join(" ",
            xml.Descendants()
                .Where(x => x.Name.LocalName == "text")
                .SelectMany(x => x.Descendants()
                    .Where(p => p.Name.LocalName == "p")
                    .Select(p => p.Value)));

        // Extrag doar topicurile articolului
        rezultat.Topics = xml.Descendants()
            .Where(x => x.Name.LocalName == "codes" &&
                (string)x.Attribute("class") == "bip:topics:1.0")
            .SelectMany(x => x.Descendants()
                .Where(c => c.Name.LocalName == "code")
                .Select(c => (string)c.Attribute("code") ?? ""))
            .Where(cod => cod.Length > 0)
            .Distinct()
            .ToList();

        // Curat textul si elimin cuvintele care nu ma intereseaza
        string text = titlu + " " + textArticol;

        text = text.ToLowerInvariant();
        text = Regex.Replace(text, @"\d+", " ");
        text = Regex.Replace(text, @"[^\p{L}\p{M}\s]", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();

        string[] cuvinte = text.Split(
            new[] { ' ' },
            StringSplitOptions.RemoveEmptyEntries);

        cuvinte = cuvinte
            .Where(w => !stopwords.Contains(w))
            .ToArray();

        // Aplic stemming pentru a obtine forma de baza aprox
        for (int i = 0; i < cuvinte.Length; i++)
        {
            cuvinte[i] = stemmer.Stem(cuvinte[i]).Value;
        }

        // Calculez de cate ori apare fiecare cuv in articol
        foreach (string cuvant in cuvinte)
        {
            if (cuvant.Length == 0)
                continue;

            if (rezultat.Counts.ContainsKey(cuvant))
                rezultat.Counts[cuvant]++;
            else
                rezultat.Counts[cuvant] = 1;
        }

        // Salvez separat textul preprocesat al articolului
        string caleArticol = Path.Combine(
            folderArticole, rezultat.Id + ".txt");

        string continutFisier =
            "Titlu: " + titlu + Environment.NewLine +
            "Topicuri: " + string.Join(" ", rezultat.Topics) +
            Environment.NewLine +
            "Text preprocesat: " + string.Join(" ", cuvinte);

        File.WriteAllText(
            caleArticol, continutFisier, Encoding.UTF8);

        return rezultat;
    }
}