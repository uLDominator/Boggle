using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using CustomNetworking;
using System.Net;
using BB;
using System.Threading;

namespace BoggleServer
{
   public class BoggleServer
    {

       private TcpListener server;
       private List<StringSocket> allSockets;
       private Queue<StringSocket> queuedSockets;

        static void Main(string[] args)
        {
            // Check for proper parameter count
            if (args.Length < 2 || args.Length > 3)
            {
                Console.WriteLine("Please enter the correct parameters: (Required)Time, (Required)PathName, (Optional)Initialize.");
                return;
            }

            // Declare and initialize first parameter, time
            int time;
            if (Int32.TryParse(args[0], out time))
            {
                if (time < 1)
                {
                    Console.WriteLine("The time given wasn't a positive integer.");
                    return;
                }
            }
            // The parameter cant be parsed as an int
            else
            {
                Console.WriteLine("The first parameter represents the time of the game, only integers are allowed.");
                return;
            }

            // Declare and initialize second parameter, legal words
            try
            {
                System.IO.StreamReader file = new System.IO.StreamReader(args[1]);
                file.Close();
            }
            catch
            {
                Console.WriteLine("The last parameter should be the filepath.");
                return;
            }
            String filePath = args[1];

            // Declare and (if given) initialize third paremeter, initialize board
            String initializeBoard = null;
            if (args.Length == 3)
            {
                // If the parameter isnt 16 chars long report an error
                if (args[2].Length != 16)
                {
                    Console.WriteLine("The third parameter must have exactly 16 letters.");
                    return;
                }
                else
                {
                    // Search for any non-letters in the parameter, if one is found report an error
                    foreach (char c in args[2])
                    {
                        if (!Char.IsLetter(c))
                        {
                            Console.WriteLine("The third parameter may only consist of letters.");
                            Console.WriteLine(c + " is not a letter");
                            return;
                        }
                    }
                    // If all are letters then initialize the board
                    initializeBoard = args[2];
                }                   
            }
            
            //Create the server
            new BoggleServer(time, filePath, initializeBoard);
            
            //Wait for user input
            Console.ReadLine();
        }

       //Member variables representing the time length of the game, the
       //filepath to the dictionary of legal words, and the board initialization
       private int time;
       private string filePath;
       private string initBoard;

        /// <summary>
        /// Creates a BoggleServer that listens for connections on the provided port
        /// </summary>
        private BoggleServer (int time, String filePath, String initializeBoard) 
        {
            //Instantiate the member variables
            this.time = time;
            this.initBoard = initializeBoard;
            this.filePath = filePath;

            // Listens for incoming connections
            server = new TcpListener(IPAddress.Any, 2000);
            allSockets = new List<StringSocket>();
            queuedSockets = new Queue<StringSocket>();

            // Start the server
            server.Start();

            // Begin accepting sockets
            server.BeginAcceptSocket(ConnectionReceived, null);                 
        }

        /// <summary>
        /// Deals with connection requests
        /// </summary>
        private void ConnectionReceived(IAsyncResult ar)
        {
            //Stop accepting
            Socket socket = server.EndAcceptSocket(ar);

            //Create a stringsocket to represent the new connection
            StringSocket ss = new StringSocket(socket, UTF8Encoding.Default);

            //Tell the socket to begin receiving
            ss.BeginReceive(NameReceived, ss);

            //Resume receiving new sockets
            server.BeginAcceptSocket(ConnectionReceived, null);
        }

        /// <summary>
        /// Receives the first line of text from the client, which contains the name of the remote
        /// user.  Uses it to compose and send back a welcome message.
        /// </summary>
        private void NameReceived(String name, Exception e, object p)
        {
            StringSocket ss = (StringSocket)p;


            if (name == null)
            {
                Console.WriteLine("Client disconnected");
                return;
            }
            // Make case insensitive
            name = name.ToUpper();
            Console.WriteLine("received " + name);
            // If the command is correct?
            if (name.StartsWith("PLAY "))
            {
                //Remove the "PLAY "
                name.Remove(0, 4);

                //Treat the rest as the users name
                ss.Name = name;

                //Lock the code to make asynchronous
                lock (allSockets)
                {
                    //Add the socket to our list of players
                    allSockets.Add(ss);

                    // If someone is already waiting, begin a game
                    if (queuedSockets.Count >= 1)
                    {
                        // Put the player in line
                        queuedSockets.Enqueue(ss);

                        // Start a game with the first two players in line
                        StringSocket player1 = queuedSockets.Dequeue();
                        StringSocket player2 = queuedSockets.Dequeue();


                        //put the the stuff we need to create a new game in a tuple
                        //tuple is sent to threadpool
                        //the threadpool creates the new game
                        Tuple<StringSocket, StringSocket, int, string, string> ThreadPoolTuple =
                            new Tuple<StringSocket, StringSocket, int, string, string>(player1, player2, time, filePath, initBoard);
                        ThreadPool.QueueUserWorkItem(this.ThreadPoolCallBack);
                        // Insert method call to begin a FUN GAME OF BOGGLE OMAGERD

                    }
                    // Queue is empty
                    else
                    {
                        // Queue the person up to wait indefinetly, potentially to be forever alone
                        queuedSockets.Enqueue(ss);
                    }
                }
            }
            else
            {

                ss.BeginSend("Ignoring " + name, (o, e1) => { }, null);
                Console.WriteLine("Sending Ignoring" + name);
            }
                
        }

       /// <summary>
       /// Helper method used to start the games. This is called when we add a thread to the threadpool, so each game
       /// has its own thread to operate on.
       /// </summary>
       /// <param name="game"></param>
       private void ThreadPoolCallBack(object game)
       {
           Tuple<StringSocket, StringSocket, int, string, string> temp = (Tuple<StringSocket,StringSocket, int, string, string>)game;

           //item 1 is player 1
           //item 2 is player 2
           //item 3 is the time
           //item 4 is the file path to the dictionary
           //item 5 is the initial string to start the game
           new BoggleGame(temp.Item1, temp.Item2, temp.Item3, temp.Item4, temp.Item5);
       }

        

        /// <summary>
        /// Deals with lines of text as they arrive at the server.
        /// </summary>
        /// <param name="line"></param>
        /// <param name="e"></param>
        /// <param name="ss"></param>
        private void IncomingCallback(String line, Exception e, object ss)
        {
            //Tell the socket to keep receiving
            ((StringSocket)ss).BeginReceive(IncomingCallback, ss);
        }
    }

   private class BoggleGame
   {
       //Member variables for each game

       //the game length time
       private int time;

       //Both players and their respective names
       private StringSocket player1;
       private String player1Name;
       private StringSocket player2;
       private String player2Name;

       //The boggle board
       private BoggleBoard bb;

       //Both players scores
       private int p1Score;
       private int p2Score;

       //The filepath for the dictionary
       private String filepath;

       //The lists that will hold all words played, which each word appearing
       //in only one list
       private HashSet<String> p1Words;
       private HashSet<String> p1IllegalWords;
       private HashSet<String> p2Words;
       private HashSet<String> p2IllegalWords;
       private HashSet<String> validWords;
       private HashSet<String> commonWords;

       /// <summary>
       /// Constructor for a boggle game. Initializes the member variables and starts the game
       /// between two given sockets (player1 and player2).
       /// </summary>
       /// <param name="player1"></param>
       /// <param name="player2"></param>
       /// <param name="time"></param>
       /// <param name="filepath"></param>
       /// <param name="initBoard"></param>
       public BoggleGame(StringSocket player1, StringSocket player2, int time, string filepath, string initBoard)
       {
           //Initialize member variables
           this.player1     = player1;
           player1Name      = player1.Name;
           this.player2     = player2;
           player2Name      = player2.Name;
           p2Words          = new HashSet<string>();
           p2IllegalWords   = new HashSet<string>();
           p1Words          = new HashSet<string>();
           p1IllegalWords   = new HashSet<string>();
           commonWords      = new HashSet<string>();
           validWords       = new HashSet<String>();
           this.time        = time;
           this.filepath    = filepath;
           p1Score          = 0;
           p2Score          = 0;
           
           Console.WriteLine("Starting game between " + player1Name + " and " + player2Name);
           string line;

           // Read the file and display it line by line.
           System.IO.StreamReader file =
              new System.IO.StreamReader(filepath);
           while ((line = file.ReadLine()) != null)
           {
               //For each line in the file, add it to the valid words list
               validWords.Add(line);               
           }

           //Close the file
           file.Close();

           //Create the board, if given an initialization, then initialize the board,
           //otherwise create a random start
           if (initBoard != null)
               bb = new BoggleBoard(initBoard);
           else
               bb = new BoggleBoard();

           //Send the START command to both players, showing the letters, time, and opponents name
           player1.BeginSend("START " + bb.ToString() + " " + time.ToString() + " " + player2.Name +"\n", (o, e) => { }, null);
           player2.BeginSend("START " + bb.ToString() + " " + time.ToString() + " " + player1.Name + "\n", (o, e) => { }, null);

           //Create a timer to keep track of the time with a 1 second interval
           System.Timers.Timer gameTimer = new System.Timers.Timer(1000);

           //Add the event handler to the timer
           gameTimer.Elapsed += new System.Timers.ElapsedEventHandler(gameTimer_Elapsed);

           //If we've run out of time
           if (time != 0)
           {
               //receive stuff from player 1
               player1.BeginReceive(Player1Input, null);
               //receive stuff fromp player 2
               player2.BeginReceive(Player2Input, null);
           }           
       }

       //Lock used to provide asynchronous play
       private readonly object inputLock = new object();

       /// <summary>
       /// Helper callback that will determine if a word played is valid. If it is valid
       /// and meets certain conditions as specified by the game of boggle, the score
       /// is updated.
       /// </summary>
       /// <param name="s"></param>
       /// <param name="e"></param>
       /// <param name="payload"></param>
       private void Player1Input(String s, Exception e, object payload) 
       {

           if(s == null && e == null && payload == null)
           {
               Console.WriteLine("Player 1 disconnected");
               player2.BeginSend("TERMINATED", (e1, o1) => { }, null);
           }
           //ignore all inputs after time expires
           if (time == 0)
               return;
            //check to see if has more three or more characters
            //then continue with checks
           if (time != 0 && s.Length >= 3 && !p1Words.Contains(s))
           {
               lock (inputLock) 
               {
                   //If the word is valid
                   if (time != 00 && validWords.Contains(s) && bb.CanBeFormed(s))
                   {
                       p1Words.Add(s);
                       //If the opponent has already played the word
                       if (p2Words.Contains(s))
                       {
                           p1Words.Remove(s);
                           commonWords.Add(s);
                       }
                       else
                       {
                           //Increment score
                           p1Score += Score(s);

                           //Do sends for both sockets
                           if (time != 0)
                           {
                               player1.BeginSend("SCORE " + p1Score + " " + p2Score + "\n", (o, e1) => { }, null);
                               player2.BeginSend("SCORE " + p2Score + " " + p1Score + "\n", (o, e1) => { }, null);
                           }
                       }
                   }
                //if it's not legal, it's illegal...
                   else
                   {
                       if (!p1IllegalWords.Contains(s))
                       {
                           p1IllegalWords.Add(s);
                           p1Score -= 1;
                       }

                       //DO sends for both sockets
                       if (time != 0)
                       {
                           player1.BeginSend("SCORE " + p1Score + " " + p2Score + "\n", (o, e1) => { }, null);
                           player2.BeginSend("SCORE " + p2Score + " " + p1Score + "\n", (o, e1) => { }, null);
                       }
                   }             
               }        
           }
           //If we still have time, continue receiving
           if(time != 0)
                player1.BeginReceive(Player1Input, null);
       }

       /// <summary>
       /// Helper callback that will determine if a word played is valid. If it is valid
       /// and meets certain conditions as specified by the game of boggle, the score
       /// is updated.
       /// </summary>
       /// <param name="s"></param>
       /// <param name="e"></param>
       /// <param name="payload"></param>
       private void Player2Input(String s, Exception e, object payload)
       {
           //ignore all inputs after time expires
           if (time == 0)
               return;
           //check to see if has more three or more characters
           if (s == null && e == null && payload == null)
           {
               Console.WriteLine("Player 2 disconnected");
               player1.BeginSend("TERMINATED", (e1, o1) => { }, null);
           }
            //then continue with checks
           if (time != 0 && s.Length >= 3 && !p2Words.Contains(s))
           {
               lock (inputLock)
               {
                   //If the word is valid
                   if (time != 00 && validWords.Contains(s) && bb.CanBeFormed(s))
                   {
                       p2Words.Add(s);
                       //If the opponent has already played the word
                       if (p1Words.Contains(s))
                       {
                           p2Words.Remove(s);
                           commonWords.Add(s);
                       }
                       else
                       {
                           //Increment score
                           p2Score += Score(s);

                           //Do sends for both sockets
                           if (time != 0)
                           {
                               player2.BeginSend("SCORE " + p2Score + " " + p1Score + "\n", (o, e1) => { }, null);
                               player1.BeginSend("SCORE " + p1Score + " " + p2Score + "\n", (o, e1) => { }, null);
                           }
                       }
                   }
                   //if it's not legal, it's illegal...
                   else
                   {
                       if (!p2IllegalWords.Contains(s))
                       {
                           p2IllegalWords.Add(s);
                           p2Score -= 1;
                       }

                       //DO sends for both sockets
                       if (time != 0)
                       {
                           player2.BeginSend("SCORE " + p2Score + " " + p1Score + "\n", (o, e1) => { }, null);
                           player1.BeginSend("SCORE " + p1Score + " " + p2Score + "\n", (o, e1) => { }, null);
                       }
                   }
               }
           }
           //If we still have time, continue receiving
           if (time != 0)
               player2.BeginReceive(Player1Input, null);
       }

       /// <summary>
       /// Helper method for determining the score for a given word in Boggle.
       /// </summary>
       /// <param name="s"></param>
       /// <returns></returns>
       private int Score(string s)
       {
           //ignore all inputs after time expires
           if (time == 0)
               return 0;
           
           //Otherwise return the score of a string of length s
           if (s.Length < 5)
               return 1;
           else if (s.Length == 5)
               return 2;
           else if (s.Length == 6)
               return 3;
           else if (s.Length == 7)
               return 4;
           else
               return 11;
       }

       /// <summary>
       /// Every one second this method will be called. It will decrement the time,
       /// report the new time to the players, then determine if the timer should be closed
       /// or remain active.
       /// </summary>
       /// <param name="sender"></param>
       /// <param name="e"></param>
       void gameTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
       {
           //Decrement time
           time--;

           //Inform both players that the time has changed
           player1.BeginSend("TIME " + time + "\n", (o, x) => { }, null);
           player2.BeginSend("TIME " + time + "\n", (o, x) => { }, null);

           System.Timers.Timer timer = (System.Timers.Timer)sender;

           //time's up
           if (time == 0)
           {

               timer.Close();


               //send score to player1 and player 2
               player1.BeginSend("SCORE " + p1Score + " " + p2Score + "\n", (o, e1) => { }, null);
               player2.BeginSend("SCORE " + p2Score + " " + p1Score + "\n", (o, e1) => { }, null);


               //send summary to player 1
               player1.BeginSend("STOP " + p1Words.Count + " " + p1Words.ToString() + " " + p2Words.Count + " " + p2Words.ToString() + " " + 
                   commonWords.Count + " " + commonWords.ToString() + " " + p1IllegalWords.Count + " " + p1IllegalWords.ToString() + " " + 
                   p2IllegalWords.Count + " " + p2IllegalWords.ToString() + "\n", (o, e1) => { }, null);
              
               //send summary to player 2
               player2.BeginSend("STOP " + p2Words.Count + " " + p2Words.ToString() + " " + p1Words.Count + " " + p1Words.ToString() + " " + 
                   commonWords.Count + " " + commonWords.ToString() + " " + p2IllegalWords.Count + " " + p2IllegalWords.ToString() + " " + 
                   p1IllegalWords.Count + " " + p1IllegalWords.ToString() + "\n", (o, e1) => { }, null);

               //Send the TERMINATE command to both users
               player1.BeginSend("TERMINATE", (o, e1) => { }, null);
               player2.BeginSend("TERMINATE", (o, e1) => { }, null);

               //Close both sockets
               player1.Close();
               player2.Close();
           }
       }
   }
}
