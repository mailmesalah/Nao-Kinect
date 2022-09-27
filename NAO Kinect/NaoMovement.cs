using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAO_Kinect
{
    [Serializable]
    class NaoMovement
    {
        public long timeDifference;
        public Processing.BodyInfo bodyInfo;

        public Processing.BodyInfo getBodyInfo()
        {
            return bodyInfo;
        }

        public void setBodyInfo(Processing.BodyInfo bodyInfo)
        {
            this.bodyInfo = bodyInfo;
        }

        public long getTimeDifference()
        {
            return timeDifference;
        }

        public void setTimeDifference(long timeDifference)
        {
            this.timeDifference = timeDifference;
        }
    }
}
