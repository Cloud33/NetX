﻿using System;
using System.Collections.Generic;
using System.Text;
using ChatServer.Models;
using Netx;

namespace Interfaces
{
    [Build]
    public interface IClient
    {
        [TAG(1001)]
        void SetUserStats(long userid, byte status);

        [TAG(1002)]
        void SayMessage(long fromuserId, string fromusername, byte MsgType, string msg,long time);

        [TAG(1003)]
        void NeedLogOn();

        [TAG(1004)]
        void UserAdd(User newuser);
    }
}
