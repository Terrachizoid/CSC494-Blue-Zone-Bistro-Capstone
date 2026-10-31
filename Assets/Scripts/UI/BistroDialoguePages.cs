// Splits long dialogue text into pages for the bottom dialogue box.
// Pages break only between sentences (a single overlong sentence falls back to word breaks), use the fewest
// pages that fit, and among those the most even split, so a page is never a two-word leftover.
// Fit is estimated from characters per line and line count (the box shows about 4 lines of ~95 characters).
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
public static class BistroDialoguePages
{
    public const int CharsPerLine=95, MaxLines=4;
    private struct Unit{public string text;public bool paragraphEnd;}
    private static readonly Regex SentenceBreak=new Regex(@"(?<=[.!?])\s+(?=[A-Z0-9\""\u201C('$])");
    public static List<string> Split(string text)
    {
        var units=Explode(Units(text));int n=units.Count;
        if(n==0)return new List<string>{""};
        // best[i] = fewest pages, then smallest sum of squared page lengths, for the first i units.
        var pages=new int[n+1];var cost=new long[n+1];var prev=new int[n+1];
        for(int i=1;i<=n;i++){pages[i]=int.MaxValue;prev[i]=-1;}
        for(int i=1;i<=n;i++)
            for(int j=i-1;j>=0;j--)
            {
                if(!Fits(units,j,i))break;
                if(pages[j]==int.MaxValue)continue;
                long length=Join(units,j,i).Length;int p=pages[j]+1;long c=cost[j]+length*length;
                if(p<pages[i]||(p==pages[i]&&c<cost[i])){pages[i]=p;cost[i]=c;prev[i]=j;}
            }
        var result=new List<string>();
        for(int i=n;i>0;i=prev[i])
        {
            if(prev[i]<0){result.Clear();result.Add(Join(units,0,n));break;} // cannot happen after Explode; safe fallback
            result.Insert(0,Join(units,prev[i],i));
        }
        return result;
    }
    private static List<Unit> Units(string text)
    {
        var list=new List<Unit>();
        foreach(var raw in (text??"").Split('\n'))
        {
            string paragraph=raw.Trim();if(paragraph.Length==0)continue;
            var sentences=SentenceBreak.Split(paragraph);
            for(int i=0;i<sentences.Length;i++)if(sentences[i].Length>0)list.Add(new Unit{text=sentences[i],paragraphEnd=i==sentences.Length-1});
        }
        return list;
    }
    // A sentence too long for one page is broken between words into page-sized pieces.
    private static List<Unit> Explode(List<Unit> units)
    {
        var list=new List<Unit>();
        foreach(var unit in units)
        {
            if(Lines(unit.text)<=MaxLines){list.Add(unit);continue;}
            string chunk="";
            foreach(var word in unit.text.Split(' '))
            {
                string next=chunk.Length==0?word:chunk+" "+word;
                if(chunk.Length>0&&Lines(next)>MaxLines){list.Add(new Unit{text=chunk});chunk=word;}else chunk=next;
            }
            list.Add(new Unit{text=chunk,paragraphEnd=unit.paragraphEnd});
        }
        return list;
    }
    private static int Lines(string paragraph)=>Mathf.Max(1,Mathf.CeilToInt(paragraph.Length/(float)CharsPerLine));
    private static bool Fits(List<Unit> units,int from,int to)
    {
        int lines=0;string paragraph="";
        for(int i=from;i<to;i++)
        {
            paragraph=paragraph.Length==0?units[i].text:paragraph+" "+units[i].text;
            if(units[i].paragraphEnd){lines+=Lines(paragraph);paragraph="";}
        }
        if(paragraph.Length>0)lines+=Lines(paragraph);
        return lines<=MaxLines;
    }
    private static string Join(List<Unit> units,int from,int to)
    {
        var text=new System.Text.StringBuilder();
        for(int i=from;i<to;i++){text.Append(units[i].text);text.Append(units[i].paragraphEnd?'\n':' ');}
        return text.ToString().Trim();
    }
}
