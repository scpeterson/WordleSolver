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

const init = {
  guesses: [initialGuess()],
  possibilities: [],
  count: 0,
  customCandidates: "",
  error: "",
  loading: false
};

let model = init;

const update = (message, state) => {
  switch (message.type) {
    case "guessChanged":
      return {
        ...state,
        guesses: state.guesses.map(g =>
          g.id === message.id ? { ...g, guess: message.value.slice(0, 5).replace(/[^a-z]/gi, "") } : g
        )
      };
    case "feedbackChanged":
      return {
        ...state,
        guesses: state.guesses.map(g =>
          g.id === message.id
            ? { ...g, feedback: g.feedback.map((f, i) => i === message.index ? nextFeedback(f) : f) }
            : g
        )
      };
    case "addGuess":
      return { ...state, guesses: [...state.guesses, initialGuess()] };
    case "removeGuess":
      return { ...state, guesses: state.guesses.filter(g => g.id !== message.id) };
    case "customCandidatesChanged":
      return { ...state, customCandidates: message.value };
    case "solving":
      return { ...state, loading: true, error: "" };
    case "solved":
      return { ...state, loading: false, error: "", count: message.count, possibilities: message.possibilities };
    case "failed":
      return { ...state, loading: false, error: message.error };
    case "reset":
      return init;
    default:
      return state;
  }
};

const dispatch = message => {
  model = update(message, model);
  view(model);
};

const nextFeedback = current => {
  const index = feedbackCycle.indexOf(current);
  return feedbackCycle[(index + 1) % feedbackCycle.length];
};

const toTokenString = feedback =>
  feedback.map(item => feedbackLabels[item]).join("");

const solve = async () => {
  dispatch({ type: "solving" });

  const candidates = model.customCandidates
    .split(/\s|,/)
    .map(w => w.trim().toLowerCase())
    .filter(Boolean);

  const body = {
    guesses: model.guesses.map(g => ({
      guess: g.guess,
      feedback: toTokenString(g.feedback)
    })),
    candidates: candidates.length > 0 ? candidates : null
  };

  try {
    const response = await fetch("/api/solve", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(body)
    });

    const payload = await response.json();

    if (!response.ok) {
      dispatch({ type: "failed", error: payload.error || "Unable to solve with that feedback." });
      return;
    }

    dispatch({ type: "solved", count: payload.count, possibilities: payload.possibilities });
  } catch {
    dispatch({ type: "failed", error: "The solver API is not reachable." });
  }
};

const el = (tag, attributes = {}, children = []) => {
  const node = document.createElement(tag);

  Object.entries(attributes).forEach(([key, value]) => {
    if (key.startsWith("on")) {
      node.addEventListener(key.slice(2).toLowerCase(), value);
    } else if (key === "className") {
      node.className = value;
    } else if (value !== null && value !== undefined) {
      node.setAttribute(key, value);
    }
  });

  children.forEach(child => {
    node.append(child instanceof Node ? child : document.createTextNode(child));
  });

  return node;
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
  const active = document.activeElement;
  const focusKey = active?.dataset?.focusKey;
  const selectionStart = typeof active?.selectionStart === "number" ? active.selectionStart : null;

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

  if (focusKey) {
    const nextActive = app.querySelector(`[data-focus-key="${focusKey}"]`);

    if (nextActive) {
      nextActive.focus();

      if (selectionStart !== null && typeof nextActive.setSelectionRange === "function") {
        nextActive.setSelectionRange(selectionStart, selectionStart);
      }
    }
  }
};

view(model);
