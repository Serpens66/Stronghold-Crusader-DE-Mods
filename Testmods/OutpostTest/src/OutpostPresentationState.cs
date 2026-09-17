namespace OutpostTest
{
    // No Unity dependency: regression-test the hot-path write/edge decisions directly.
    internal sealed class OutpostPresentationState
    {
        private int frame=-1,clickFrame=-1;
        private bool visible,positionKnown;
        private float x,y,z;
        internal bool EnterFrame(int current) { if(frame==current)return false;frame=current;return true; }
        internal bool Click(int current,bool down) {
            if(!down || clickFrame==current)return false;clickFrame=current;return true;
        }
        internal bool Visibility(bool next) { if(visible==next)return false;visible=next;return true; }
        internal bool Position(float a,float b,float c) {
            if(positionKnown && x==a && y==b && z==c)return false;
            x=a;y=b;z=c;positionKnown=true;return true;
        }
        internal void ForgetPosition() { positionKnown=false; }
        internal static bool Selection(int mode,int panel,int id,int snapshotId) => mode==16 && panel==45 && id>0 && id<=3999 && id==snapshotId;
    }
}