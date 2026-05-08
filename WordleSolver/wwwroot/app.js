const feedbackCycle = ["absent", "present", "correct"];
const feedbackLabels = {
  absent: "B",
  present: "Y",
  correct: "G"
};

const initialGuess = () => ({
  id: crypto.randomUUID(),
  guess: "",
  feedback: ["absent", "absent", "absent", "absent", "absent"]
});

const initialModel = () => ({
  guesses: [initialGuess()],
  possibilities: [],
  count: 0,
  customCandidates: "",
  error: "",
  loading: false
});

const cleanGuess = value =>
  value
    .slice(0, 5)
    .replace(/[^a-z]/gi, "");

const parseCandidates = value =>
  value
    .split(/\s|,/)
    .map(word => word.trim().toLowerCase())
    .filter(Boolean);

const updateGuess = (guesses, id, transform) =>
  guesses.map(guess =>
    guess.id === id ? transform(guess) : guess
  );

const updateFeedbackAt = (feedback, index, transform) =>
  feedback.map((item, itemIndex) =>
    itemIndex === index ? transform(item) : item
  );

const nextFeedback = current => {
  const index = feedbackCycle.indexOf(current);
  return feedbackCycle[(index + 1) % feedbackCycle.length];
};

const toTokenString = feedback =>
  feedback.map(item => feedbackLabels[item]).join("");

const guessToRequest = guess => ({
  guess: guess.guess,
  feedback: toTokenString(guess.feedback)
});

const solveRequest = state => {
  const candidates = parseCandidates(state.customCandidates);

  return {
    guesses: state.guesses.map(guessToRequest),
    candidates: candidates.length > 0 ? candidates : null
  };
};

let model = initialModel();

const update = (message, state) => {
  switch (message.type) {
    case "guessChanged":
      return {
        ...state,
        guesses: updateGuess(state.guesses, message.id, guess => (
          { ...guess, guess: cleanGuess(message.value) }
        ))
      };
    case "feedbackChanged":
      return {
        ...state,
        guesses: updateGuess(state.guesses, message.id, guess => (
          {
            ...guess,
            feedback: updateFeedbackAt(guess.feedback, message.index, nextFeedback)
          }
        ))
      };
    case "addGuess":
      return {
        ...state,
        guesses: [...state.guesses, initialGuess()]
      };
    case "removeGuess":
      return {
        ...state,
        guesses: state.guesses.filter(guess => guess.id !== message.id)
      };
    case "customCandidatesChanged":
      return {
        ...state,
        customCandidates: message.value
      };
    case "solving":
      return {
        ...state,
        loading: true,
        error: ""
      };
    case "solved":
      return {
        ...state,
        loading: false,
        error: "",
        count: message.count,
        possibilities: message.possibilities
      };
    case "failed":
      return {
        ...state,
        loading: false,
        error: message.error
      };
    case "reset":
      return initialModel();
    default:
      return state;
  }
};

const dispatch = message => {
  model = update(message, model);
  view(model);
};

const responseToMessage = async response => {
  const payload = await response.json();

  if (!response.ok) {
    return {
      type: "failed",
      error: payload.error || "Unable to solve with that feedback."
    };
  }

  return {
    type: "solved",
    count: payload.count,
    possibilities: payload.possibilities
  };
};

const solve = async () => {
  dispatch({ type: "solving" });

  try {
    const response = await fetch("/api/solve", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(solveRequest(model))
    });

    dispatch(await responseToMessage(response));
  } catch {
    dispatch({ type: "failed", error: "The solver API is not reachable." });
  }
};

const attributeEntries = attributes =>
  Object.entries(attributes)
    .filter(([, value]) => value !== null && value !== undefined);

const setAttribute = node => ([key, value]) => {
  if (key.startsWith("on")) {
    node.addEventListener(key.slice(2).toLowerCase(), value);
  } else if (key === "className") {
    node.className = value;
  } else {
    node.setAttribute(key, value);
  }
};

const toNode = child =>
  child instanceof Node ? child : document.createTextNode(child);

const el = (tag, attributes = {}, children = []) => {
  const node = document.createElement(tag);

  attributeEntries(attributes).forEach(setAttribute(node));
  children.map(toNode).forEach(child => node.append(child));

  return node;
};

const inputSelection = element =>
  typeof element?.selectionStart === "number" ? element.selectionStart : null;

const focusSnapshot = () => {
  const active = document.activeElement;

  return {
    focusKey: active?.dataset?.focusKey,
    selectionStart: inputSelection(active)
  };
};

const restoreFocus = (container, snapshot) => {
  if (!snapshot.focusKey) {
    return;
  }

  const nextActive = container.querySelector(`[data-focus-key="${snapshot.focusKey}"]`);

  if (!nextActive) {
    return;
  }

  nextActive.focus();

  if (snapshot.selectionStart !== null && typeof nextActive.setSelectionRange === "function") {
    nextActive.setSelectionRange(snapshot.selectionStart, snapshot.selectionStart);
  }
};

const guessView = (state, guess) =>
  el("section", { className: "guess-row" }, [
    el("input", {
      className: "word-input",
      value: guess.guess,
      maxlength: "5",
      placeholder: "GUESS",
      "data-focus-key": `guess-${guess.id}`,
      "aria-label": "Guess",
      onInput: event => dispatch({ type: "guessChanged", id: guess.id, value: event.target.value })
    }),
    el("div", { className: "feedback-grid" },
      guess.feedback.map((feedback, index) =>
        el("button", {
          className: `tile ${feedback}`,
          title: "Cycle feedback",
          "aria-label": `Feedback position ${index + 1}`,
          onClick: () => dispatch({ type: "feedbackChanged", id: guess.id, index })
        }, [feedbackLabels[feedback]])
      )
    ),
    state.guesses.length > 1
      ? el("button", { className: "remove", onClick: () => dispatch({ type: "removeGuess", id: guess.id }) }, ["Remove"])
      : el("span")
  ]);

const view = state => {
  const app = document.querySelector("#app");
  const snapshot = focusSnapshot();

  app.replaceChildren(
    el("div", { className: "shell" }, [
      el("section", { className: "panel" }, [
        el("h1", {}, ["Wordle Solver"]),
        el("p", { className: "lede" }, ["Enter each guess and click feedback tiles until they match Wordle: G green, Y yellow, B gray."]),
        el("div", { className: "guess-list" }, state.guesses.map(guess => guessView(state, guess))),
        el("div", { className: "actions" }, [
          el("button", { className: "primary", onClick: solve }, [state.loading ? "Solving..." : "Solve"]),
          el("button", { className: "secondary", onClick: () => dispatch({ type: "addGuess" }) }, ["Add Guess"]),
          el("button", { className: "secondary", onClick: () => dispatch({ type: "reset" }) }, ["Reset"])
        ]),
        el("p", { className: "error", role: "status" }, [state.error]),
        el("label", { className: "candidate-input" }, [
          el("span", { className: "hint" }, ["Optional candidate words"]),
          el("textarea", {
            value: state.customCandidates,
            "data-focus-key": "custom-candidates",
            placeholder: "Paste five-letter answers separated by spaces, commas, or new lines.",
            onInput: event => dispatch({ type: "customCandidatesChanged", value: event.target.value })
          })
        ])
      ]),
      el("section", { className: "results" }, [
        el("div", { className: "summary" }, [
          el("div", {}, [
            el("div", { className: "count" }, [String(state.count)]),
            el("div", { className: "hint" }, ["possible answers"])
          ])
        ]),
        el("div", { className: "word-grid" }, state.possibilities.map(word =>
          el("div", { className: "word" }, [word])
        ))
      ])
    ])
  );

  restoreFocus(app, snapshot);
};

view(model);
