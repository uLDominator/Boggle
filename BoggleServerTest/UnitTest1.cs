using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Sockets;
using CustomNetworking;
using System.Threading;

namespace BoggleServerTest
{
    /// <summary>
    /// Summary description for UnitTest1
    /// </summary>
    [TestClass]
    public class UnitTest1
    {
        public UnitTest1()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void test1()
        {
            new Test1Class().run();
        }

        
        public class Test1Class 
        {

            private ManualResetEvent mre;
            public void run()
            {
                TcpClient client = null;
                try
                {

                    client = new TcpClient("localhost", 2000);

                    // Obtain the sockets from the two ends of the connection.  We are using the blocking AcceptSocket()
                    // method here, which is OK for a test case.
                    Socket clientSocket = client.Client;
                    // Wrap the two ends of the connection into StringSockets
                    StringSocket ss = new StringSocket(clientSocket, new UTF8Encoding());
                    mre = new ManualResetEvent(false);
                    ss.BeginSend("PLAY Happy GillMore", (o, e) => { }, null);



                }

                catch (Exception e)
                { 
                
                }
            }   
        }

    }
}
