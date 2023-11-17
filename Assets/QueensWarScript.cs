using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text.RegularExpressions;
using System;

public class QueensWarScript : MonoBehaviour {

    public KMAudio audio;
    public KMBombInfo bomb;
    public KMSelectable[] buttons;
    public GameObject[] displays;
    public GameObject cardPrefab;
    public Sprite[] cardSprites;
    public Sprite[] specialCardSprites;
    public TextMesh stageText;
    public Transform solveAnimPivot;
    public Transform[] solveAnimCardPos;
    public MeshRenderer backing;
    public Material[] backMats;

    List<GameObject> generatedCards = new List<GameObject>();
    List<int> cardIndexes = new List<int>();
    List<int> pressedCardOrder = new List<int>();
    List<int> counts = new List<int> { 0, 0, 0, 0 };
    string[] ignoredModules;
    string[] ranks = { "Ace", "2", "3", "4", "5", "6", "7", "8", "9", "10", "Jack", "Queen", "King" };
    string[] suitOrder = { "Spades", "Hearts", "Diamonds", "Clubs" };
    int[] cardOrder = { 0, 1, 2, 3 };
    int maxStages;
    int solveCount;
    int solveQueue = 1;
    int mode = -1;
    int stageCounter;
    bool firstCard = true;
    bool animating;
    bool realSolve;

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    void Awake()
    {
        moduleId = moduleIdCounter++;
        foreach (KMSelectable obj in buttons)
        {
            KMSelectable pressed = obj;
            pressed.OnInteract += delegate () { PressCard(pressed); return false; };
        }
        GetComponent<KMBombModule>().OnActivate += Activate;
    }

    void Start()
    {
        cardOrder = cardOrder.Shuffle();
        for (int i = 0; i < displays.Length; i++)
            displays[i].SetActive(false);
        ignoredModules = GetComponent<KMBossModule>().GetIgnoredModules("Queen’s War", new string[]{
                "14",
                "Forget Enigma",
                "Forget Everything",
                "Forget It Not",
                "Forget Me Later",
                "Forget Me Not",
                "Forget Perspective",
                "Forget Them All",
                "Forget This",
                "Forget Us Not",
                "Organization",
                "Purgatory",
                "Queen's War",
                "Simon's Stages",
                "Souvenir",
                "Tallordered Keys",
                "The Time Keeper",
                "Timing is Everything",
                "The Troll",
                "Turn The Key",
                "Übermodule",
                "Ültimate Custom Night",
                "The Very Annoying Button"
            });
        maxStages = bomb.GetSolvableModuleNames().Where(a => !ignoredModules.Contains(a)).ToList().Count;
    }

    void Update()
    {
        if (mode == 0 && solveCount != bomb.GetSolvedModuleNames().Where(a => !ignoredModules.Contains(a)).ToList().Count)
        {
            solveQueue += bomb.GetSolvedModuleNames().Where(a => !ignoredModules.Contains(a)).ToList().Count - solveCount;
            solveCount = bomb.GetSolvedModuleNames().Where(a => !ignoredModules.Contains(a)).ToList().Count;
            if (solveCount == maxStages)
                mode = 1;
        }
    }

    void Activate()
    {
        if (Application.isEditor)
            maxStages = bomb.GetSolvableModuleNames().Where(a => !ignoredModules.Contains(a)).ToList().Count;
        Debug.LogFormat("[Queen’s War #{0}] Number of non-ignored modules detected: {1}", moduleId, maxStages);
        if (maxStages == 0)
        {
            moduleSolved = true;
            GetComponent<KMBombModule>().HandlePass();
            Debug.LogFormat("[Queen’s War #{0}] Autosolving module since no stages can be generated", moduleId);
            return;
        }
        mode = 0;
        StartCoroutine(HandleQueue());
        for (int i = 0; i < 2; i++)
            displays[i].SetActive(true);
    }

    void PressCard(KMSelectable pressed)
    {
        if (moduleSolved != true && animating != true && mode == 1)
        {
            pressed.AddInteractionPunch();
            int index = Array.IndexOf(buttons, pressed);
            Debug.LogFormat("[Queen’s War #{0}] Pressed {1}", moduleId, suitOrder[cardOrder[index]]);
            if (pressedCardOrder.Contains(index))
            {
                Debug.LogFormat("[Queen’s War #{0}] Strike! The {1} suit was already pressed", moduleId, suitOrder[cardOrder[index]]);
                GetComponent<KMBombModule>().HandleStrike();
                pressedCardOrder.Clear();
                StartCoroutine(StrikeRecovery());
                return;
            }
            pressedCardOrder.Add(index);
            if (pressedCardOrder.Count == 4)
            {
                int lastVal = -1;
                for (int i = 0; i < 4; i++)
                {
                    if (i != 0 && counts[cardOrder[pressedCardOrder[i]]] > counts[lastVal])
                    {
                        Debug.LogFormat("[Queen’s War #{0}] Strike! The count of the {1} suit is greater than the {2} suit", moduleId, suitOrder[cardOrder[pressedCardOrder[i]]], suitOrder[lastVal]);
                        GetComponent<KMBombModule>().HandleStrike();
                        pressedCardOrder.Clear();
                        StartCoroutine(StrikeRecovery());
                        return;
                    }
                    else
                        lastVal = cardOrder[pressedCardOrder[i]];
                }
                moduleSolved = true;
                displays[0].SetActive(false);
                StartCoroutine(HandleSolveAnim());
            }
        }
    }

    IEnumerator HandleQueue()
    {
        while (mode == 0 || solveQueue > 0)
        {
            yield return null;
            if (solveQueue > 0)
            {
                solveQueue--;
                if (stageCounter == maxStages)
                    break;
                stageCounter++;
                stageText.text = stageCounter.ToString();
                cardIndexes.Add(UnityEngine.Random.Range(0, cardSprites.Length));
                Debug.LogFormat("[Queen’s War #{0}] <Stage #{1}> The placed card was the {2} of {3}", moduleId, stageCounter, ranks[cardIndexes.Last() % 13], suitOrder[cardIndexes.Last() / 13]);
                if (firstCard)
                {
                    cardPrefab.GetComponent<SpriteRenderer>().sprite = cardSprites[cardIndexes.Last()];
                    generatedCards.Add(cardPrefab);
                    firstCard = false;
                }
                else
                {
                    GameObject newCard = Instantiate(cardPrefab, displays[1].transform);
                    newCard.GetComponent<SpriteRenderer>().sprite = cardSprites[cardIndexes.Last()];
                    newCard.GetComponent<SpriteRenderer>().sortingOrder = stageCounter + 1;
                    generatedCards.Add(newCard);
                    if (cardIndexes.Last() % 13 == cardIndexes[cardIndexes.Count - 2] % 13)
                    {
                        int stored = counts[3];
                        counts.RemoveAt(3);
                        counts.Insert(0, stored);
                        Debug.LogFormat("[Queen’s War #{0}] <Stage #{1}> This card matches rank with the previous, the suit counts have cycled one to the right", moduleId, stageCounter);
                    }
                    else if (cardIndexes.Last() % 13 > cardIndexes[cardIndexes.Count - 2] % 13)
                    {
                        counts[cardIndexes.Last() / 13]++;
                        Debug.LogFormat("[Queen’s War #{0}] <Stage #{1}> This card has a greater rank than the previous, the {2} suit now has a count of {3}", moduleId, stageCounter, suitOrder[cardIndexes.Last() / 13], counts[cardIndexes.Last() / 13]);
                    }
                }
                StartCoroutine(PlaceCard(generatedCards.Last()));
                yield return new WaitForSecondsRealtime(1.5f);
            }
        }
        Debug.LogFormat("[Queen’s War #{0}] Final suit counts in order are: {1}, {2}, {3}, {4}", moduleId, counts[0], counts[1], counts[2], counts[3]);
        StartCoroutine(TransitionAnim());
    }

    IEnumerator PlaceCard(GameObject card)
    {
        audio.PlaySoundAtTransform("place", transform);
        float[] pos = { -0.1f, 0, 0.1f };
        int[] choices = { 1, 1 };
        while ((choices[0] == 1 && choices[1] == 1) || (choices[0] == 2 && choices[1] == 2))
        {
            for (int i = 0; i < choices.Length; i++)
                choices[i] = UnityEngine.Random.Range(0, pos.Length);
        }
        card.transform.localPosition = new Vector3(pos[choices[0]], 0.03f, pos[choices[1]]);
        card.SetActive(true);
        Vector3 startPos = card.transform.localPosition;
        Vector3 endPos = new Vector3(0, 0.01f, 0);
        Vector3 startRot = new Vector3(90, UnityEngine.Random.Range(0, 360), 0);
        Vector3 endRot = new Vector3(90, UnityEngine.Random.Range(0, 360), 0);
        float t = 0f;
        while (t < 1f)
        {
            yield return null;
            t += Time.deltaTime * 3f;
            card.transform.localPosition = Vector3.Lerp(startPos, endPos, t);
            card.transform.localEulerAngles = Vector3.Lerp(startRot, endRot, t);
        }
    }

    IEnumerator TransitionAnim()
    {
        animating = true;
        List<Vector3> locations = new List<Vector3>();
        float[] pos = { -0.1f, 0, 0.1f };
        for (int i = 0; i < generatedCards.Count; i++)
        {
            int[] choices = { 1, 1 };
            while ((choices[0] == 1 && choices[1] == 1) || (choices[0] == 2 && choices[1] == 2))
            {
                for (int j = 0; j < choices.Length; j++)
                    choices[j] = UnityEngine.Random.Range(0, pos.Length);
            }
            locations.Add(new Vector3(pos[choices[0]], 0.03f, pos[choices[1]]));
        }
        float t = 0f;
        Vector3 startPos = new Vector3(0, 0.01f, 0);
        while (t < 1f)
        {
            yield return null;
            t += Time.deltaTime * 3f;
            for (int i = 0; i < generatedCards.Count; i++)
                generatedCards[i].transform.localPosition = Vector3.Lerp(startPos, locations[i], t);
        }
        for (int i = 0; i < generatedCards.Count; i++)
            generatedCards[i].SetActive(false);
        displays[0].SetActive(false);
        Vector3[] startPositions = new Vector3[4];
        Vector3[] endPositions = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            endPositions[i] = buttons[i].gameObject.transform.localPosition;
            if (i < 2)
                buttons[i].gameObject.transform.localPosition = new Vector3(buttons[i].gameObject.transform.localPosition.x, 0.05f, buttons[i].gameObject.transform.localPosition.z + .1f);
            else
                buttons[i].gameObject.transform.localPosition = new Vector3(buttons[i].gameObject.transform.localPosition.x, 0.05f, buttons[i].gameObject.transform.localPosition.z - .1f);
            startPositions[i] = buttons[i].gameObject.transform.localPosition;
            buttons[i].gameObject.GetComponent<SpriteRenderer>().sprite = specialCardSprites[cardOrder[i]];
        }
        displays[2].SetActive(true);
        audio.PlaySoundAtTransform("place", transform);
        t = 0f;
        while (t < 1f)
        {
            yield return null;
            t += Time.deltaTime * 3f;
            for (int i = 0; i < 4; i++)
                buttons[i].gameObject.transform.localPosition = Vector3.Lerp(startPositions[i], endPositions[i], t);
        }
        animating = false;
    }

    IEnumerator HandleSolveAnim()
    {
        audio.PlaySoundAtTransform("solve", transform);
        StartCoroutine(HandleSolveAnim2());
        Vector3[] startPositions = new Vector3[4];
        for (int i = 0; i < 4; i++)
            startPositions[i] = buttons[i].gameObject.transform.localPosition;
        float t = 0f;
        while (t < 1f)
        {
            yield return null;
            t += Time.deltaTime * 3f;
            for (int i = 0; i < 4; i++)
                buttons[i].gameObject.transform.localPosition = Vector3.Lerp(startPositions[i], solveAnimCardPos[i].localPosition, t);
        }
    }

    IEnumerator HandleSolveAnim2()
    {
        float t = 0f;
        while (t < 6.15f)
        {
            yield return null;
            t += Time.deltaTime;
            solveAnimPivot.Rotate(Vector3.up * ((50f + (120 * t)) * Time.deltaTime));
            for (int i = 0; i < 4; i++)
                buttons[i].gameObject.transform.localEulerAngles = new Vector3(90, -solveAnimPivot.localEulerAngles.y, 0);
        }
        displays[2].SetActive(false);
        backing.material = backMats[1];
        yield return new WaitForSecondsRealtime(0.85f);
        backing.material = backMats[0];
        displays[3].SetActive(true);
        GetComponent<KMBombModule>().HandlePass();
        Debug.LogFormat("[Queen’s War #{0}] Module solved", moduleId);
        realSolve = true;
    }

    IEnumerator StrikeRecovery()
    {
        mode = 2;
        cardOrder = cardOrder.Shuffle();
        Vector3[] startPositions = new Vector3[4];
        Vector3[] endPositions = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            startPositions[i] = buttons[i].gameObject.transform.localPosition;
            if (i < 2)
                endPositions[i] = new Vector3(startPositions[i].x, 0.05f, startPositions[i].z + .1f);
            else
                endPositions[i] = new Vector3(startPositions[i].x, 0.05f, startPositions[i].z - .1f);
        }
        float t = 0f;
        while (t < 1f)
        {
            yield return null;
            t += Time.deltaTime * 3f;
            for (int i = 0; i < 4; i++)
                buttons[i].gameObject.transform.localPosition = Vector3.Lerp(startPositions[i], endPositions[i], t);
        }
        displays[2].SetActive(false);
        displays[0].SetActive(true);
        for (int j = 0; j < maxStages; j++)
        {
            generatedCards[j].SetActive(true);
            audio.PlaySoundAtTransform("place", transform);
            stageText.text = (j + 1).ToString();
            t = 0f;
            Vector3 startPos = generatedCards[j].transform.localPosition;
            Vector3 endPos = new Vector3(0, 0.01f, 0);
            Vector3 startRot = new Vector3(90, UnityEngine.Random.Range(0, 360), 0);
            Vector3 endRot = new Vector3(90, UnityEngine.Random.Range(0, 360), 0);
            while (t < 1f)
            {
                yield return null;
                t += Time.deltaTime * 3f;
                generatedCards[j].transform.localPosition = Vector3.Lerp(startPos, endPos, t);
                generatedCards[j].transform.localEulerAngles = Vector3.Lerp(startRot, endRot, t);
            }
            yield return new WaitForSecondsRealtime(1.5f);
        }
        for (int i = 0; i < 4; i++)
            buttons[i].gameObject.transform.localPosition = startPositions[i];
        StartCoroutine(TransitionAnim());
        mode = 1;
    }

    //twitch plays
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} press <p> (p2)... [Presses the card in the specified position (optionally include multiple positions)] | Valid positions are TL, TR, BL, BR, or 1-4 in reading order";
    #pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        string[] parameters = command.Split(' ');
        if (Regex.IsMatch(parameters[0], @"^\s*press\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            if (parameters.Length == 1)
                yield return "sendtochaterror Please specify at least 1 position!";
            else
            {
                for (int i = 1; i < parameters.Length; i++)
                {
                    if (!parameters[i].ToLowerInvariant().EqualsAny("tl", "tr", "bl", "br", "1", "2", "3", "4"))
                    {
                        yield return "sendtochaterror!f The specified position '" + parameters[i] + "' is invalid!";
                        yield break;
                    }
                }
                if (mode != 1)
                {
                    yield return "sendtochaterror The four suit cards are not currently present!";
                    yield break;
                }
                yield return null;
                for (int i = 1; i < parameters.Length; i++)
                {
                    if (parameters[i].ToLowerInvariant().EqualsAny("tl", "1"))
                        buttons[0].OnInteract();
                    else if (parameters[i].ToLowerInvariant().EqualsAny("tr", "2"))
                        buttons[1].OnInteract();
                    else if (parameters[i].ToLowerInvariant().EqualsAny("bl", "3"))
                        buttons[2].OnInteract();
                    else
                        buttons[3].OnInteract();
                    if (moduleSolved)
                    {
                        yield return "solve";
                        break;
                    }
                    yield return new WaitForSeconds(.1f);
                }
            }
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        if (!moduleSolved)
        {
            while (mode != 1 || solveQueue > 0 || animating) yield return true;
            if (pressedCardOrder.Count > 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (!pressedCardOrder.Contains(i) && counts[cardOrder[pressedCardOrder.Last()]] < counts[cardOrder[i]])
                    {
                        GetComponent<KMBombModule>().HandlePass();
                        moduleSolved = true;
                        yield break;
                    }
                }
                int lastVal = -1;
                for (int i = 0; i < pressedCardOrder.Count; i++)
                {
                    if (i != 0 && counts[cardOrder[pressedCardOrder[i]]] > counts[lastVal])
                    {
                        GetComponent<KMBombModule>().HandlePass();
                        moduleSolved = true;
                        yield break;
                    }
                    else
                        lastVal = cardOrder[pressedCardOrder[i]];
                }
            }
            for (int i = pressedCardOrder.Count; i < 4; i++)
            {
                int max = -1;
                int index = -1;
                for (int j = 0; j < 4; j++)
                {
                    if (!pressedCardOrder.Contains(j) && counts[cardOrder[j]] > max)
                    {
                        max = counts[cardOrder[j]];
                        index = j;
                    }
                }
                buttons[index].OnInteract();
                yield return new WaitForSeconds(.1f);
            }
        }
        while (!realSolve) yield return true;
    }
}