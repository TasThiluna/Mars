using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;
using rnd = UnityEngine.Random;

public class mars : MonoBehaviour
{
    public KMBombInfo bomb;
    public new KMAudio audio;
    public KMBombModule module;

    public KMSelectable hideButton;
    public GameObject planet;
    public KMSelectable planetButton;
    public GameObject background;
    public Transform pivot;

    private string[] currentMatrix;
    private int[] destinations = new int[3];
    private int[] speeds = new int[3];
    private float[] travelTimes = new float[3];
    private int wordIndex;
    private int[] letterOrder;
    private int voiceIndex;
    private bool modulePresent;
    private int timesPressed;

    private string[] tableA;
    private string[] tableB;
    private static readonly string alphabet = "ABDEFGHIJKLMNOPQRSTUVWXYZ";
    private static readonly string[] directionNames = new[] { "up", "left", "down", "right" };
    private static readonly float[] endPoints = new[] { 90f, 0f, 270f, 180f };
    private static readonly string[] speedNames = new[] { "less than", "exactly", "more than" };
    private static readonly string[] martianDictionary = new[] { "WRINT", "BOMPE", "RINTU", "MUNPO", "EYTAH", "DUNBI", "RAHTU", "OOSLA", "KORBS", "QIBAS", "ZUTUN", "RAJAL", "UFNOR", "GLIMP", "ZULBA", "QORKT", "YVMEL", "HOXZI", "RILBO", "JIHTR", "UNUNT", "KAXNE", "LUTIV", "IPROM" };
    private static readonly string[] englishDictionary = new[] { "color", "LED", "wire", "Simon", "arrow", "identification", "button", "key", "symbol", "binary", "cipher", "cycle", "forget", "bean", "cruel", "faulty", "talk", "memory", "maze", "double", "grid", "dial", "code", "number" };

    private bool visible = true;
    private bool isAnimating = false;
    private bool submissionMode;
    private bool cantPress;
    private bool timerStarted;
    private bool speaking;
    private Coroutine orbCycleAnimation;
    private Coroutine transmission;
    public AudioClip[] allSounds;

    private static int moduleIdCounter = 1;
    private int moduleId;
    private bool moduleSolved;

    private void Awake()
    {
        moduleId = moduleIdCounter++;
        planetButton.OnInteract += delegate () { PressPlanetButton(); return false; };
        hideButton.OnInteract += delegate () { StartCoroutine(HidePlanet()); return false; };
        tableA = "HDOMXPSJYWREUNBQGVZKILTAF".ToArray().Select(ch => ch.ToString()).ToArray();
        tableB = "23|12|35|45|24|15|14|25|↺|↻|34|13".Split('|');
    }

    private void Start()
    {
        StartCoroutine(PlanetRotation());
        currentMatrix = tableA.ToArray();
        destinations = Enumerable.Range(0, 4).ToList().Shuffle().Take(3).ToArray();
        speeds = new int[3].Select(x => x = rnd.Range(0, 3)).ToArray();
        Debug.LogFormat("[Mars #{0}] Movements in order: {1}.", moduleId, Enumerable.Range(0, 3).Select(x => (x == 0 ? "T" : "t") + string.Format("o {0} in {1} 1 second", directionNames[destinations[x]], speedNames[speeds[x]])).Join(", "));
        for (int i = 0; i < 3; i++)
        {
            switch (speeds[i])
            {
                case 0:
                    travelTimes[i] = new[] { .1f, .25f, .5f }.PickRandom();
                    break;
                case 1:
                    travelTimes[i] = 1f;
                    break;
                case 2:
                    travelTimes[i] = new[] { 1.5f, 2f, 5f }.PickRandom();
                    break;
            }
        }
        pivot.localEulerAngles = new Vector3(0f, endPoints[Enumerable.Range(0, 4).First(x => !destinations.Contains(x))], 0f);
        orbCycleAnimation = StartCoroutine(OrbCycle());

        for (int i = 0; i < 3; i++)
        {
            var instruction = tableB[destinations[i] * 3 + speeds[i]];
            Debug.LogFormat("[Mars #{0}] Row {1} column {2} 1 second: instruction {3} obtained.", moduleId, directionNames[destinations[i]], speedNames[speeds[i]], instruction);
            switch (instruction)
            {
                case "↺":
                    Debug.LogFormat("[Mars #{0}] Rotate the grid counterclockwise.", moduleId);
                    currentMatrix = Rotate(currentMatrix, 1);
                    break;
                case "↻":
                    Debug.LogFormat("[Mars #{0}] Rotate the grid clockwise.", moduleId);
                    currentMatrix = Rotate(currentMatrix, 3);
                    break;
                default:
                    var rows = i % 2 == 0;
                    Debug.LogFormat("[Mars #{0}] {1} step in the process, swap the {2}s.", moduleId, rows ? "Odd" : "Even", rows ? "row" : "column");
                    var ix1 = int.Parse(instruction[0].ToString()) - 1;
                    var ix2 = int.Parse(instruction[1].ToString()) - 1;
                    if (rows)
                    {
                        for (int j = 0; j < 5; j++)
                        {
                            var s1 = currentMatrix[ix1 * 5 + j];
                            var s2 = currentMatrix[ix2 * 5 + j];
                            currentMatrix[ix1 * 5 + j] = s2;
                            currentMatrix[ix2 * 5 + j] = s1;
                        }
                    }
                    else
                    {
                        for (int j = 0; j < 5; j++)
                        {
                            var s1 = currentMatrix[j * 5 + ix1];
                            var s2 = currentMatrix[j * 5 + ix2];
                            currentMatrix[j * 5 + ix1] = s2;
                            currentMatrix[j * 5 + ix2] = s1;
                        }
                    }
                    break;
            }
        }
        Debug.LogFormat("[Mars #{0}] New matrix after transformations: {1}", moduleId, currentMatrix.Join(", "));
        var sum = bomb.GetSerialNumberNumbers().Sum();
        Debug.LogFormat("[Mars #{0}] Caesar shift the matrix by {1}.", moduleId, sum);
        currentMatrix = currentMatrix.Select(ch => alphabet[(alphabet.IndexOf(ch) + sum) % 25].ToString()).ToArray();
        Debug.LogFormat("[Mars #{0}] New matrix after Caesar shifting: {1}", moduleId, currentMatrix.Join(", "));

        voiceIndex = rnd.Range(0, 3);
        wordIndex = rnd.Range(0, martianDictionary.Length);
        letterOrder = Enumerable.Range(0, 5).ToList().Shuffle().ToArray();
        Debug.LogFormat("[Mars #{0}] The martian word being spoken is {1} (transmitted as {2}), which translates to {3}.", moduleId, martianDictionary[wordIndex].ToLowerInvariant(), letterOrder.Select(x => martianDictionary[wordIndex][x]).Join(""), englishDictionary[wordIndex]);
        var allModules = bomb.GetModuleNames();
        switch (englishDictionary[wordIndex])
        {
            case "LED":
                modulePresent = allModules.Any(str => str.Contains("LED"));
                break;
            case "cruel":
                modulePresent = allModules.Select(str => str.ToLowerInvariant()).Any(str => str.Contains("cruel") || str.Contains("complicated"));
                break;
            case "faulty":
                modulePresent = allModules.Select(str => str.ToLowerInvariant()).Any(str => str.Contains("faulty") || str.Contains("broken"));
                break;
            case "color":
                modulePresent = allModules.Select(str => str.ToLowerInvariant()).Any(str => str.Contains("color") || str.Contains("colour") || str.Contains("colo(u)r"));
                break;
            default:
                modulePresent = allModules.Select(str => str.ToLowerInvariant()).Any(str => str.Contains(englishDictionary[wordIndex]));
                break;
        }
        Debug.LogFormat("[Mars #{0}] There is{1} a module containing the translated word.", moduleId, modulePresent ? "" : " not");
    }

    private IEnumerator PlanetRotation()
    {
        var elapsed = 0f;
        while (true)
        {
            planet.transform.localEulerAngles = new Vector3(elapsed / 45 * 360f, 90f, 90f);
            yield return null;
            elapsed += Time.deltaTime;
        }
    }

    private IEnumerator OrbCycle()
    {
        while (true)
        {
            for (int i = 0; i < 3; i++)
            {
                var elapsed = 0f;
                var duration = travelTimes[i];
                var start = pivot.localRotation;
                var end = Quaternion.Euler(0f, endPoints[destinations[i]], 0f);
                while (elapsed < duration)
                {
                    pivot.localRotation = Quaternion.Slerp(start, end, elapsed / duration);
                    yield return null;
                    elapsed += Time.deltaTime;
                }
                pivot.localRotation = end;
                yield return new WaitForSeconds(i == 2 ? 3f : .5f);
                while (submissionMode)
                    yield return null;
            }
        }
    }

    private IEnumerator HidePlanet()
    {
        if (isAnimating) yield break;
        isAnimating = true;
        while (background.transform.localPosition.y < 0.08)
        {
            background.transform.localPosition += new Vector3(0, 0.0025f, 0);
            background.transform.localScale += new Vector3(0, 0.005f, 0);
            yield return null;
        }
        visible = !visible;
        planet.SetActive(visible);
        planetButton.gameObject.SetActive(visible);
        yield return new WaitForSecondsRealtime(0.5f);
        while (background.transform.localPosition.y > -0.008)
        {
            background.transform.localPosition -= new Vector3(0, 0.0025f, 0);
            background.transform.localScale -= new Vector3(0, 0.005f, 0);
            yield return null;
        }
        Debug.LogFormat("<Mars #{0}> Visible toggled to {1}.", moduleId, visible);
        yield return null;
        isAnimating = false;
    }

    private void PressPlanetButton()
    {
        if (cantPress || moduleSolved)
            return;
        if (!submissionMode)
        {
            submissionMode = true;
            Debug.LogFormat("[Mars #{0}] Playing the transmission and entering submission mode.", moduleId);
            transmission = StartCoroutine(TransmitMessage());
        }
        else
        {
            timesPressed++;
            if (!timerStarted)
            {
                timerStarted = true;
                StartCoroutine(CountUp());
            }
        }
    }

    private void CheckSubmission()
    {
        switch (timesPressed)
        {
            case 1:
                Debug.LogFormat("[Mars #{0}] One press detected.", moduleId);
                if (!modulePresent)
                {
                    Debug.LogFormat("[Mars #{0}] That was correct. Module solved!", moduleId);
                    module.HandlePass();
                    StopCoroutine(orbCycleAnimation);
                    orbCycleAnimation = null;
                    StartCoroutine(HandleStopTransmission());
                    audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
                    moduleSolved = true;
                }
                else
                {
                    Debug.LogFormat("[Mars #{0}] That was incorrect. Strike!", moduleId);
                    module.HandleStrike();
                    submissionMode = false;
                    timerStarted = false;
                    cantPress = false;
                    timesPressed = 0;
                    transmission = StartCoroutine(TransmitMessage());
                }
                break;
            case 2:
                Debug.LogFormat("[Mars #{0}] Two presses detected.", moduleId);
                if (modulePresent)
                {
                    Debug.LogFormat("[Mars #{0}] That was correct. Module solved!", moduleId);
                    module.HandlePass();
                    StopCoroutine(orbCycleAnimation);
                    orbCycleAnimation = null;
                    StartCoroutine(HandleStopTransmission());
                    audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
                    moduleSolved = true;
                }
                else
                {
                    Debug.LogFormat("[Mars #{0}] That was incorrect. Strike!", moduleId);
                    module.HandleStrike();
                    submissionMode = false;
                    timerStarted = false;
                    cantPress = false;
                    timesPressed = 0;
                }
                break;
            default:
                Debug.LogFormat("[Mars #{0}] Three or more presses detected, exiting submission mode.", moduleId);
                submissionMode = false;
                timerStarted = false;
                cantPress = false;
                timesPressed = 0;
                break;
        }
    }

    private IEnumerator TransmitMessage()
    {
        for (int i = 0; i < 5; i++)
        {
            var letter = martianDictionary[wordIndex][letterOrder[i]].ToString();
            var nameA = voiceIndex + "-" + (Array.IndexOf(currentMatrix, letter) / 5);
            var nameB = voiceIndex + "-" + (Array.IndexOf(currentMatrix, letter) % 5);
            speaking = true;
            audio.PlaySoundAtTransform(nameA, transform);
            yield return new WaitForSeconds(allSounds.First(clip => clip.name == nameA).length + .5f);
            speaking = false;
            speaking = true;
            audio.PlaySoundAtTransform(nameB, transform);
            yield return new WaitForSeconds(allSounds.First(clip => clip.name == nameB).length + .5f);
            speaking = false;
        }
    }

    private IEnumerator HandleStopTransmission()
    {
        yield return new WaitUntil(() => !speaking);
        StopCoroutine(transmission);
        transmission = null;
    }

    private IEnumerator CountUp()
    {
        yield return new WaitForSeconds(1f);
        CheckSubmission();
    }

    private static string[] Rotate(string[] grid, int numberOfTimes) // CCW
    {
        for (var n = 0; n < numberOfTimes; n++)
            grid = grid.Select((_, i) => grid[(i % 5) * 5 + 4 - (i / 5)]).ToArray();
        return grid;
    }

    // Twitch Plays
#pragma warning disable 414
    private readonly string TwitchHelpMessage = "!{0} start [Plays the transmission and enters submission mode] !{0} yes [In submission mode, presses the orb twice in one second] !{0} no [In submission mode, presses the orb once in one second] !{0} cancel [In submission mode, press the orb 3 times in one second] !{0} hide [Presses the orb to hide or unhide the planet]";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string input)
    {
        input = input.Trim().ToLowerInvariant();
        if (input == "start")
        {
            if (submissionMode)
            {
                yield return "sendtochaterror You're already in submission mode. Dummy.";
                yield break;
            }
            yield return null;
            planetButton.OnInteract();
        }
        else if (input == "yes")
        {
            if (!submissionMode)
            {
                yield return "sendtochaterror Can't do that when not in submission mode. Dummy.";
                yield break;
            }
            yield return null;
            planetButton.OnInteract();
            yield return new WaitForSeconds(.5f);
            planetButton.OnInteract();
        }
        else if (input == "no")
        {
            if (!submissionMode)
            {
                yield return "sendtochaterror Can't do that when not in submission mode. Dummy.";
                yield break;
            }
            yield return null;
            planetButton.OnInteract();
            yield return new WaitForSeconds(1f);
        }
        else if (input == "cancel")
        {
            if (!submissionMode)
            {
                yield return "sendtochaterror Can't do that when not in submission mode. Dummy.";
                yield break;
            }
            yield return null;
            for (int i = 0; i < 3; i++)
            {
                planetButton.OnInteract();
                yield return new WaitForSeconds(.2f);
            }
        }
        else if (input == "hide")
        {
            yield return null;
            hideButton.OnInteract();
        }
        else
            yield break;
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        if (timesPressed == 1)
        {
            if (modulePresent)
            {
                planetButton.OnInteract();
                yield return null;
                planetButton.OnInteract();
            }
        }
        if (timesPressed == 2)
        {
            if (!modulePresent)
            {
                yield return null;
                planetButton.OnInteract();
            }
        }
        yield return new WaitUntil(() => timesPressed == 0);
        if (moduleSolved)
            yield break;
        planetButton.OnInteract();
        yield return new WaitForSeconds(.2f);
        planetButton.OnInteract();
        if (modulePresent)
        {
            yield return new WaitForSeconds(.2f);
            planetButton.OnInteract();
        }
        while (!moduleSolved)
        {
            yield return true;
            yield return null;
        }
    }

}
