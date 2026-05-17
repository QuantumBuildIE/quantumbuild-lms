"use client";

import { useState, useRef, useEffect, useCallback } from "react";
import { useAuth, useHasAnyPermission } from "@/lib/auth/use-auth";
import { apiClient } from "@/lib/api/client";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { Send, Bot } from "lucide-react";
import { cn } from "@/lib/utils";
import { format } from "date-fns";

// ============================================
// Types
// ============================================

type MessageRole = "user" | "assistant";

interface Message {
  id: string;
  role: MessageRole;
  content: string;
  timestamp: Date;
}

interface TopicShortcut {
  label: string;
  prompt: string;
}

// ============================================
// Markdown renderer
// ============================================

function renderMarkdown(text: string): React.ReactNode {
  const lines = text.split("\n");
  const elements: React.ReactNode[] = [];
  let listItems: string[] = [];
  let key = 0;

  const flushList = () => {
    if (listItems.length > 0) {
      elements.push(
        <ul key={key++} className="my-1.5 ml-4 list-disc space-y-0.5">
          {listItems.map((item, i) => (
            <li key={i} className="text-sm leading-relaxed">
              {renderInline(item)}
            </li>
          ))}
        </ul>
      );
      listItems = [];
    }
  };

  for (const line of lines) {
    const listMatch = line.match(/^[-*]\s+(.*)/);
    const numberedMatch = line.match(/^\d+\.\s+(.*)/);

    if (listMatch) {
      listItems.push(listMatch[1]);
    } else if (numberedMatch) {
      listItems.push(numberedMatch[1]);
    } else {
      flushList();
      if (line.trim() === "") {
        elements.push(<div key={key++} className="h-1.5" />);
      } else if (line.startsWith("### ")) {
        elements.push(
          <p key={key++} className="text-sm font-semibold mt-2">
            {renderInline(line.slice(4))}
          </p>
        );
      } else if (line.startsWith("## ")) {
        elements.push(
          <p key={key++} className="text-sm font-bold mt-2">
            {renderInline(line.slice(3))}
          </p>
        );
      } else {
        elements.push(
          <p key={key++} className="text-sm leading-relaxed">
            {renderInline(line)}
          </p>
        );
      }
    }
  }

  flushList();
  return <>{elements}</>;
}

function renderInline(text: string): React.ReactNode {
  const parts = text.split(/(\*\*[^*]+\*\*|`[^`]+`)/g);
  return parts.map((part, i) => {
    if (part.startsWith("**") && part.endsWith("**")) {
      return <strong key={i}>{part.slice(2, -2)}</strong>;
    }
    if (part.startsWith("`") && part.endsWith("`")) {
      return (
        <code key={i} className="rounded bg-muted px-1 py-0.5 font-mono text-xs">
          {part.slice(1, -1)}
        </code>
      );
    }
    return part;
  });
}

// ============================================
// Shortcut groups
// ============================================

const EMPLOYEE_SHORTCUTS: TopicShortcut[] = [
  { label: "Find my assigned training", prompt: "Where do I find my assigned training?" },
  { label: "Retry a failed quiz", prompt: "How do I retry a failed quiz?" },
  { label: "View my certificates", prompt: "Where are my certificates?" },
  { label: "Video won't play", prompt: "My video won't play" },
  { label: "Training showing as overdue", prompt: "Why is my training showing as overdue?" },
];

const SUPERVISOR_SHORTCUTS: TopicShortcut[] = [
  { label: "Assign an operator to my team", prompt: "How do I assign an operator to my team?" },
  { label: "Read the Skills Matrix", prompt: "How do I read the Skills Matrix?" },
  { label: "See team's overdue training", prompt: "How do I see my team's overdue training?" },
];

const ADMIN_SHORTCUTS: TopicShortcut[] = [
  { label: "Create a new Toolbox Talk", prompt: "How do I create a new Toolbox Talk?" },
  { label: "Schedule training for my team", prompt: "How do I schedule training for my team?" },
  { label: "Run a translation validation", prompt: "How do I run a translation validation?" },
  { label: "Find the compliance reports", prompt: "Where are the compliance reports?" },
  { label: "Add a new employee", prompt: "How do I add a new employee?" },
  { label: "Assign training to an employee", prompt: "How do I assign training to an employee?" },
];

const SUPERUSER_SHORTCUTS: TopicShortcut[] = [
  { label: "Run a corpus run", prompt: "How do I run a corpus run?" },
  { label: "Ingest a regulatory document", prompt: "How do I ingest a regulatory document?" },
];

const WELCOME_MESSAGE: Message = {
  id: "welcome",
  role: "assistant",
  content:
    "Hi, I am Cert — your CertifiedIQ assistant. Ask me anything about using the platform. I can help with navigation, workflows, and troubleshooting.",
  timestamp: new Date(),
};

// ============================================
// Component
// ============================================

export function HelpAssistant() {
  const { user } = useAuth();
  const isAdmin = useHasAnyPermission(["ToolboxTalks.Admin", "Core.Admin"]);
  const isSuperUser = user?.isSuperUser ?? false;
  const isSupervisor = user?.roles?.includes("Supervisor") ?? false;

  const [messages, setMessages] = useState<Message[]>([WELCOME_MESSAGE]);
  const [input, setInput] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const bottomRef = useRef<HTMLDivElement>(null);

  const shortcuts: TopicShortcut[] = isSuperUser
    ? [...ADMIN_SHORTCUTS, ...SUPERUSER_SHORTCUTS]
    : isAdmin
    ? ADMIN_SHORTCUTS
    : isSupervisor
    ? [...EMPLOYEE_SHORTCUTS, ...SUPERVISOR_SHORTCUTS]
    : EMPLOYEE_SHORTCUTS;

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages, isLoading]);

  const autoResize = useCallback(() => {
    const el = textareaRef.current;
    if (!el) return;
    el.style.height = "auto";
    el.style.height = `${Math.min(el.scrollHeight, 140)}px`;
  }, []);

  const sendMessage = useCallback(
    async (text: string) => {
      const trimmed = text.trim();
      if (!trimmed || isLoading) return;

      const userMessage: Message = {
        id: crypto.randomUUID(),
        role: "user",
        content: trimmed,
        timestamp: new Date(),
      };

      const nextMessages = [...messages, userMessage];
      setMessages(nextMessages);
      setInput("");
      if (textareaRef.current) {
        textareaRef.current.style.height = "auto";
      }
      setIsLoading(true);

      try {
        const history = nextMessages.map((m) => ({ role: m.role, content: m.content }));
        const response = await apiClient.post<{ message: string }>("/help/chat", {
          messages: history,
        });

        setMessages((prev) => [
          ...prev,
          {
            id: crypto.randomUUID(),
            role: "assistant",
            content: response.data.message,
            timestamp: new Date(),
          },
        ]);
      } catch {
        setMessages((prev) => [
          ...prev,
          {
            id: crypto.randomUUID(),
            role: "assistant",
            content: "Sorry, I couldn't reach the help service. Please try again in a moment.",
            timestamp: new Date(),
          },
        ]);
      } finally {
        setIsLoading(false);
      }
    },
    [messages, isLoading]
  );

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      sendMessage(input);
    }
  };

  return (
    <div className="flex h-[calc(100vh-3.5rem-50px)] overflow-hidden">
      {/* Sidebar */}
      <aside className="hidden w-[280px] shrink-0 border-r bg-muted/30 md:flex md:flex-col">
        <div className="border-b px-4 py-4">
          <div className="flex items-center gap-2">
            <Bot className="h-5 w-5 text-primary" />
            <span className="text-sm font-semibold">Quick Questions</span>
          </div>
          <p className="mt-1 text-xs text-muted-foreground">
            Click a topic to get started
          </p>
        </div>
        <div className="flex-1 overflow-y-auto">
          <div className="space-y-1 p-3">
            {shortcuts.map((shortcut) => (
              <button
                key={shortcut.prompt}
                onClick={() => sendMessage(shortcut.prompt)}
                disabled={isLoading}
                className="w-full rounded-md px-3 py-2 text-left text-sm text-foreground transition-colors hover:bg-accent hover:text-accent-foreground disabled:pointer-events-none disabled:opacity-50"
              >
                {shortcut.label}
              </button>
            ))}
          </div>
        </div>
      </aside>

      {/* Main chat area */}
      <div className="flex flex-1 flex-col overflow-hidden">
        {/* Header */}
        <div className="border-b px-6 py-4">
          <h1 className="text-lg font-semibold">Help Assistant</h1>
          <p className="text-sm text-muted-foreground">
            Ask Cert anything about using CertifiedIQ
          </p>
        </div>

        {/* Messages */}
        <div className="flex-1 overflow-y-auto px-4 py-4">
          <div className="mx-auto max-w-2xl space-y-4">
            {messages.map((message) => (
              <MessageBubble key={message.id} message={message} />
            ))}
            {isLoading && <TypingIndicator />}
            <div ref={bottomRef} />
          </div>
        </div>

        {/* Input area */}
        <div className="border-t bg-background px-4 py-3">
          <div className="mx-auto flex max-w-2xl items-end gap-2">
            <Textarea
              ref={textareaRef}
              value={input}
              onChange={(e) => {
                setInput(e.target.value);
                autoResize();
              }}
              onKeyDown={handleKeyDown}
              placeholder="Ask a question… (Enter to send, Shift+Enter for new line)"
              rows={1}
              disabled={isLoading}
              className="max-h-[140px] min-h-[40px] resize-none overflow-y-auto py-2 leading-relaxed"
            />
            <Button
              size="icon"
              onClick={() => sendMessage(input)}
              disabled={isLoading || !input.trim()}
              className="shrink-0"
            >
              <Send className="h-4 w-4" />
            </Button>
          </div>
          <p className="mx-auto mt-1.5 max-w-2xl text-center text-xs text-muted-foreground">
            Cert may make mistakes. Always verify critical safety information.
          </p>
        </div>
      </div>
    </div>
  );
}

// ============================================
// Sub-components
// ============================================

function MessageBubble({ message }: { message: Message }) {
  const isUser = message.role === "user";

  return (
    <div className={cn("flex", isUser ? "justify-end" : "justify-start")}>
      <div
        className={cn(
          "max-w-[80%] rounded-lg px-4 py-3",
          isUser
            ? "border-l-4 border-primary bg-primary/10"
            : "border-l-4 border-amber-500 bg-muted"
        )}
      >
        {isUser ? (
          <p className="text-sm leading-relaxed">{message.content}</p>
        ) : (
          <div>{renderMarkdown(message.content)}</div>
        )}
        <p className="mt-1.5 text-right text-[10px] text-muted-foreground">
          {format(message.timestamp, "HH:mm")}
        </p>
      </div>
    </div>
  );
}

function TypingIndicator() {
  return (
    <div className="flex justify-start">
      <div className="rounded-lg border-l-4 border-amber-500 bg-muted px-4 py-3">
        <div className="flex items-center gap-1">
          {[0, 1, 2].map((i) => (
            <span
              key={i}
              className="inline-block h-1.5 w-1.5 animate-bounce rounded-full bg-muted-foreground"
              style={{ animationDelay: `${i * 150}ms` }}
            />
          ))}
        </div>
      </div>
    </div>
  );
}
