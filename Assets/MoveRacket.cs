using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
//using System.math;

using System.IO;
using System.Threading.Tasks;

public class MoveRacket : MonoBehaviour
{

    public float speed = 30;

    byte[] data = new byte[1024];
    string input, stringData;

    Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);  //实现 Berkeley 套接字接口
    public IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);  //定义服务端

    EndPoint Remote;

    int recv;
    float barHeight = 0.0f;
    float init_data = 0.0f;
    float emgfilter = 0.0f;
    float bayesfilter = 0.0f;

    static readonly object lockObject = new object();
    string returnData = "";
    bool precessData = false;

    EmgModule myEmg = new EmgModule();
    BayesFilter myBayesian = new BayesFilter();
    Simulator mySimulator = new Simulator();

    public GameObject obj;
    public Renderer rend;



    List<float> listToHoldData;
    List<float> listToHoldTime;
    List<float> listToHoldInit;
    List<float> listToHoldemgfilter;
    List<float> listToHoldbayesfilter;
    List<float> listToHoldstoredata;


    float[] a1 = { 1f, 1.7600f, 1.1829f, 0.2781f };
    float[] b1 = { 0.0181f, -0.0543f, 0.0543f, -0.0181f };
    //float[] a2 = {1f,-0.5772f, 0.4218f, -0.0563f};
    //float[] b2 = {0.0985f, 0.2956f, 0.2956f, 0.0985f};//20Hz低通滤波
    //float[] a2 = { 1f, -1.7600f, 1.1829f, -0.2781f };
    //float[] b2 = { 0.0181f, 0.0543f, 0.0543f, 0.0181f };//10Hz低通滤波
    //float[] a2 = { 1f, -2.3741f, 1.9294f, -0.5321f };
    //float[] b2 = { 0.0029f, 0.0087f, 0.0087f, 0.0029f };//5Hz低通滤波
    //float[] a2 = { 1f, -2.7488f, 2.5282f, -0.7776f };
    //float[] b2 = { 0.0002f, 0.0007f, 0.0007f, 0.0002f };//2Hz低通滤波
    float[] a2 = { 1f, -2.8744f, 2.7565f, -0.8819f };
    float[] b2 = { 0.00003f, 0.00009f, 0.00009f, 0.00003f };//1Hz低通滤波
    float[] x = new float[4];
    float[] y1 = new float[4];//高通
    float[] y2 = new float[4];//整流、
    float[] y3 = new float[4];//低通
    float[] aa = new float[10];

    //float[] save = new float[100];
    



    void Start()
    {

        Console.WriteLine("This is a Client, host name is {0}", Dns.GetHostName());//获取本地计算机的主机名
        IPEndPoint ip = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8001);


        string welcome = "你好! ";
        data = Encoding.ASCII.GetBytes(welcome);  //数据类型转换
        server.SendTo(data, data.Length, SocketFlags.None, ip);  //发送给指定服务端

        Remote = (EndPoint)sender;
        recv = server.ReceiveFrom(data, ref Remote);//获取客户端，获取客户端数据，用引用给客户端赋值 
        data = new byte[1024];


        listToHoldData = new List<float>();
        listToHoldTime = new List<float>();
        listToHoldInit = new List<float>();
        listToHoldemgfilter = new List<float>();
        listToHoldbayesfilter = new List<float>();
        listToHoldstoredata = new List<float>();

        //thread = new Thread(new ThreadStart(ThreadMethod));
        //thread.Start();

        obj = GameObject.Find("MoveRacket");





        myEmg.startEmg();




    }
    //private float ButterFilter(int jieshu, float emg)
    //{
    //    int fs = 500,fp = 20;
    //    float wp = (float)fp / (fs / 2);
    //    float aa,bb;
    //    for (int i=0;i<jieshu;i++)
    //    {
    //        save[i] = emg;
    //    }
    //    [aa,bb]=Butter(save, emg);
    //    return 
    //} 



    //private float EmgAverage(int jieshu, float emg)
    //{

    //    float sum = 0;
    //    float filterEmg = 0;
    //    for (int i = jieshu - 1; i > 0; i--)
    //    {
    //        aa[i] = aa[i - 1];
    //        sum += aa[i];
    //    }
    //    aa[0] = emg;
    //    //if (a[jieshu - 1] == 0) myEmg.emgData[0] = 0;
    //    filterEmg = (sum + aa[0]) / jieshu;
    //    return filterEmg;


    //}
    private float EmgFilter(float emg)
    {
        for (int i = 3; i > 0; i--)
        {
            x[i] = x[i - 1];
            y1[i] = y1[i - 1];

        }
        x[0] = emg;
        y1[0] = (b1[3] * x[3] + b1[2] * x[2] + b1[1] * x[1] + b1[0] * x[0] - (a1[3] * y1[3] + a1[2] * y1[2] + a1[1] * y1[1])) / a1[0];

        for (int i = 3; i > 0; i--)
        {
            y2[i] = y2[i - 1];
            y3[i] = y3[i - 1];
        }
        y2[0] = Math.Abs(y1[0]);
        y3[0] = (b2[3] * y2[3] + b2[2] * y2[2] + b2[1] * y2[1] + b2[0] * y2[0] - (a2[3] * y3[3] + a2[2] * y3[2] + a2[1] * y3[1])) / a2[0];

        return y3[0];


    }

    public float ChooseMode(string aa)
    {
        float chemg;
        switch (aa)
        {
            case "real":
                chemg = myEmg.emgData[0];
                //return chemg;
                break;


            case "sim":
                chemg =(float) mySimulator.storedata();
                break; 
            


            default:
                chemg = 0;
                break;
        }
        return chemg;


    }



    void FixedUpdate()
    {
        server.SendTo(Encoding.ASCII.GetBytes("H"), Remote);//发送信息
        data = new byte[1024];//对data清零
        recv = server.ReceiveFrom(data, ref Remote);//获取客户端，获取服务端端数据，用引用给服务端赋值，实际上服务端已经定义好并不需要赋值
        stringData = Encoding.ASCII.GetString(data, 0, recv);//字节数组转换为字符串  //输出接收到的数据 
        Console.WriteLine(stringData);


        

        float v = Input.GetAxisRaw("Vertical");
        float barHeight =0.03f * Convert.ToInt32(stringData) - 0.1f;
        barHeight = barHeight*0.25f; 



        //float init_data = myMode.ChooseMode("real");
        init_data = ChooseMode("real") ;

        if (init_data == 0)
            bayesfilter = 0;
        else
           
            //bayesfilter = (float)(myBayesian.UpdateEst(init_data/ 100 ));
            bayesfilter = (float)(myBayesian.UpdateEst(init_data * 80000 / 25));//200
            init_data = init_data * 80000;



        //emgfilter = EmgAverage(10,Math.Abs(myEmg.emgData[0]));


        //emgfilter = EmgFilter(myEmg.emgData[0]);
        emgfilter = EmgFilter(init_data);
        //emgfilter = emgfilter * 50000;

        GetComponent<Rigidbody2D>().position = new Vector2(0, barHeight);
        //GetComponent<Rigidbody2D>().position = new Vector2(0, bayesfilter*50);

        //obj.transform.position = new Vector2(0, barHeight);
        //print(barHeight*1000000);

        listToHoldData.Add(barHeight);
        //float t = Time.time;
        listToHoldTime.Add(Time.time);

        listToHoldInit.Add(init_data);
        listToHoldemgfilter.Add(emgfilter);
        listToHoldbayesfilter.Add(bayesfilter);
        listToHoldstoredata.Add(init_data);
        //Vector2 mousePosition = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
        //GetComponent<Rigidbody2D>().position = new Vector2(0, mousePosition.y);


    }

    private void OnApplicationQuit()
    {
        myEmg.stopEmg();



        string data = "";
        StreamWriter writer = new StreamWriter("test.csv", false, Encoding.UTF8);
        //writer.WriteLine(string.Format("{0},{1}", "Time", "Pressure"));
        writer.WriteLine(string.Format("{0},{1},{2},{3},{4}", "Time", "Pressure", "Init_data","EmgFilter","BayesFilter"));

        using (var e1 = listToHoldTime.GetEnumerator())
        using (var e2 = listToHoldData.GetEnumerator())
        using (var e3 = listToHoldInit.GetEnumerator())
        using (var e4 = listToHoldemgfilter.GetEnumerator())
        using (var e5 = listToHoldbayesfilter.GetEnumerator())
        {
            while (e1.MoveNext() && e2.MoveNext() && e3.MoveNext() && e4.MoveNext() && e5.MoveNext())
            {
                var item1 = e1.Current;
                var item2 = e2.Current;
                var item3 = e3.Current;
                var item4 = e4.Current;
                var item5 = e5.Current;
                data += item1.ToString();
                data += ",";
                data += item2.ToString();
                data += ",";
                data += item3.ToString();
                data += ",";
                data += item4.ToString();
                data += ",";
                data += item5.ToString();
                data += "\n";
                // use item1 and item2
            }
        }


        writer.Write(data);

        writer.Close();


        string store = "";
       
        StreamWriter writer1 = new StreamWriter("storedata.csv", false, Encoding.UTF8);
        //writer.WriteLine(string.Format("{0},{1}", "Time", "Pressure"));
       
        using (var e1 = listToHoldstoredata.GetEnumerator())
        {
            while (e1.MoveNext())
            {
                var item1 = e1.Current;
           
                store += item1.ToString();

                store += "\n";
                // use item1 and item2
            }
        }


        writer1.Write(store);

        writer1.Close();




    }



}
