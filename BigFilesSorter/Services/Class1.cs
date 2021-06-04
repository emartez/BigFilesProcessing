using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BigFilesSorter.Services
{
    public class Class1
    {
        public static async Task Split(string file)
        {
            int split_num = 1;
            //StreamWriter sw = new StreamWriter(
            //  string.Format("e:\\data\\generated\\split{0:d5}.dat", split_num));
            long read_line = 0;
            using (StreamReader sr = new StreamReader(file))
            {
                while (sr.Peek() >= 0)
                {
                    //// Progress reporting
                    //if (++read_line % 5000 == 0)
                    //    Console.Write("{0:f2}%   \r",
                    //      100.0 * sr.BaseStream.Position / sr.BaseStream.Length);

                    // Copy a line
                    var line = await sr.ReadLineAsync();
                    //await sw.WriteLineAsync(line);

                    // If the file is big, then make a new split,
                    // however if this was the last line then don't bother
                    //if (sw.BaseStream.Length > 100000000 && sr.Peek() >= 0)
                    //{
                    //    sw.Close();
                    //    split_num++;
                    //    sw = new StreamWriter(
                    //      string.Format("e:\\data\\generated\\split{0:d5}.dat", split_num));
                    //}
                }
            }
            Console.WriteLine("Finish");
            //sw.Close();
        }

        static void SortTheChunks()
        {
            foreach (string path in Directory.GetFiles("C:\\", "split*.dat"))
            {
                // Read all lines into an array
                string[] contents = File.ReadAllLines(path);
                // Sort the in-memory array
                Array.Sort(contents);
                // Create the 'sorted' filename
                string newpath = path.Replace("split", "sorted");
                // Write it
                File.WriteAllLines(newpath, contents);
                // Delete the unsorted chunk
                File.Delete(path);
                // Free the in-memory sorted array
                contents = null;
                GC.Collect();
            }
        }

        static void MergeTheChunks()
        {
            string[] paths = Directory.GetFiles("C:\\", "sorted*.dat");
            int chunks = paths.Length; // Number of chunks
            int recordsize = 100; // estimated record size
            int records = 10000000; // estimated total # records
            int maxusage = 500000000; // max memory usage
            int buffersize = maxusage / chunks; // bytes of each queue
            double recordoverhead = 7.5; // The overhead of using Queue<>
            int bufferlen = (int)(buffersize / recordsize /
              recordoverhead); // number of records in each queue

            // Open the files
            StreamReader[] readers = new StreamReader[chunks];
            for (int i = 0; i < chunks; i++)
                readers[i] = new StreamReader(paths[i]);

            // Make the queues
            Queue<string>[] queues = new Queue<string>[chunks];
            for (int i = 0; i < chunks; i++)
                queues[i] = new Queue<string>(bufferlen);

            // Load the queues
            for (int i = 0; i < chunks; i++)
                LoadQueue(queues[i], readers[i], bufferlen);

            // Merge!
            StreamWriter sw = new StreamWriter("C:\\BigFileSorted.txt");
            bool done = false;
            int lowest_index, j, progress = 0;
            string lowest_value;
            while (!done)
            {
                // Report the progress
                if (++progress % 5000 == 0)
                    Console.Write("{0:f2}%   \r",
                      100.0 * progress / records);

                // Find the chunk with the lowest value
                lowest_index = -1;
                lowest_value = "";
                for (j = 0; j < chunks; j++)
                {
                    if (queues[j] != null)
                    {
                        if (lowest_index < 0 ||
                          String.CompareOrdinal(
                            queues[j].Peek(), lowest_value) < 0)
                        {
                            lowest_index = j;
                            lowest_value = queues[j].Peek();
                        }
                    }
                }

                // Was nothing found in any queue? We must be done then.
                if (lowest_index == -1) { done = true; break; }

                // Output it
                sw.WriteLine(lowest_value);

                // Remove from queue
                queues[lowest_index].Dequeue();
                // Have we emptied the queue? Top it up
                if (queues[lowest_index].Count == 0)
                {
                    LoadQueue(queues[lowest_index],
                      readers[lowest_index], bufferlen);
                    // Was there nothing left to read?
                    if (queues[lowest_index].Count == 0)
                    {
                        queues[lowest_index] = null;
                    }
                }
            }
            sw.Close();

            // Close and delete the files
            for (int i = 0; i < chunks; i++)
            {
                readers[i].Close();
                File.Delete(paths[i]);
            }
        }

        static void LoadQueue(Queue<string> queue,
          StreamReader file, int records)
        {
            for (int i = 0; i < records; i++)
            {
                if (file.Peek() < 0) break;
                queue.Enqueue(file.ReadLine());
            }
        }
    }
}
