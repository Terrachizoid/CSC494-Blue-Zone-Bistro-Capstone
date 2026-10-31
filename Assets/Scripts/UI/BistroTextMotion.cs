// Procedural text motion on legacy UI Text. Two moves, both driven by a start time kept by the caller, so a
// screen rebuild mid-animation continues where it was instead of restarting:
// - Typewriter: letters appear one by one (short pauses after punctuation). Hidden letters are drawn
//   transparent with a rich-text colour tag, so line wrapping never jumps while the text grows.
// - Pop: a quick scale-settle for a label whose value just changed (wallet, popularity, day...).
using UnityEngine;
using UnityEngine.UI;
public sealed class BistroTextMotion : MonoBehaviour
{
    private Text text;
    private string full;
    private float start;
    private float[] times;
    private bool pop;
    private const float PopSeconds=.22f, PopScale=1.12f;
    // Seconds until the whole string is visible.
    public static float Duration(string value,float charsPerSecond,bool pauses)
    {
        var t=RevealTimes(value,charsPerSecond,pauses);
        return t.Length==0?0:t[t.Length-1];
    }
    private static float[] RevealTimes(string value,float charsPerSecond,bool pauses)
    {
        value=value??"";var times=new float[value.Length];float clock=0,step=1f/Mathf.Max(1,charsPerSecond);
        for(int i=0;i<value.Length;i++)
        {
            clock+=step;times[i]=clock;
            if(!pauses)continue;
            char c=value[i];
            if(c=='.'||c=='!'||c=='?')clock+=.18f;else if(c==','||c==';'||c==':')clock+=.08f;else if(c=='\n')clock+=.10f;
        }
        return times;
    }
    public static void Typewriter(Text text,string value,float startTime,float charsPerSecond,bool pauses)
    {
        if(text==null||string.IsNullOrEmpty(value))return;
        var motion=Get(text);motion.full=value;motion.start=startTime;motion.times=RevealTimes(value,charsPerSecond,pauses);motion.pop=false;
        text.supportRichText=true;motion.LateUpdate();
    }
    public static void Pop(Text text,float startTime)
    {
        if(text==null||Time.unscaledTime-startTime>=PopSeconds)return;
        var motion=Get(text);motion.pop=true;motion.start=startTime;motion.LateUpdate();
    }
    private static BistroTextMotion Get(Text text)
    {
        var motion=text.GetComponent<BistroTextMotion>();if(motion==null)motion=text.gameObject.AddComponent<BistroTextMotion>();
        motion.text=text;motion.enabled=true;return motion;
    }
    private void LateUpdate()
    {
        float elapsed=Time.unscaledTime-start;
        if(pop)
        {
            float k=Mathf.Clamp01(elapsed/PopSeconds);
            transform.localScale=Vector3.one*Mathf.Lerp(PopScale,1,1-(1-k)*(1-k));
            if(k>=1){transform.localScale=Vector3.one;enabled=false;}
            return;
        }
        if(times==null||full==null){enabled=false;return;}
        int shown=0;while(shown<times.Length&&times[shown]<=elapsed)shown++;
        if(shown>=full.Length){text.text=full;enabled=false;return;}
        text.text=full.Substring(0,shown)+"<color=#00000000>"+full.Substring(shown)+"</color>";
    }
    private void OnDisable(){if(pop)transform.localScale=Vector3.one;}
}
