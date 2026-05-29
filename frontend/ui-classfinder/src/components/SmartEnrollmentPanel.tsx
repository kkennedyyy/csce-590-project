import { useEffect, useMemo, useState } from 'react';

import { requestSmartEnrollment } from '../api/api';
import type { ScheduledClass, SmartEnrollmentCandidate, SmartEnrollmentResponse } from '../types';
import styles from './SmartEnrollmentPanel.module.css';

interface SmartEnrollmentPanelProps {
  studentId: string;
  previewCandidateId?: string | null;
  onPreviewCandidate: (candidate: SmartEnrollmentCandidate | null) => void;
  onAcceptCandidate: (candidate: ScheduledClass[]) => Promise<void>;
}

const PROMPT_EXAMPLES = [
  'Build me a Tuesday/Thursday schedule with CSCE331 and no Friday classes.',
  'I need MATH200, prefer one history elective, and want to finish by 3pm.',
  'Give me a balanced morning schedule with 20 minute breaks and cybersecurity-related electives.',
];

export function SmartEnrollmentPanel({
  studentId,
  previewCandidateId,
  onPreviewCandidate,
  onAcceptCandidate,
}: SmartEnrollmentPanelProps): JSX.Element {
  const [prompt, setPrompt] = useState('');
  const [response, setResponse] = useState<SmartEnrollmentResponse | null>(null);
  const [selectedCandidateId, setSelectedCandidateId] = useState('');
  const [loading, setLoading] = useState(false);
  const [accepting, setAccepting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const selectedCandidate = useMemo(
    () => response?.candidates.find((candidate) => candidate.id === selectedCandidateId) ?? response?.candidates[0] ?? null,
    [response, selectedCandidateId],
  );

  useEffect(() => {
    if (!response) {
      onPreviewCandidate(null);
      return;
    }

    const nextPreview =
      response.candidates.find((candidate) => candidate.id === (previewCandidateId || selectedCandidate?.id))
      ?? selectedCandidate
      ?? null;
    onPreviewCandidate(nextPreview);
  }, [onPreviewCandidate, previewCandidateId, response, selectedCandidate]);

  async function handleGenerate(): Promise<void> {
    setLoading(true);
    setError(null);
    try {
      const nextResponse = await requestSmartEnrollment({
        studentId,
        prompt,
        candidateLimit: 4,
      });
      setResponse(nextResponse);
      setSelectedCandidateId(nextResponse.candidates[0]?.id ?? '');
      if (nextResponse.candidates.length === 0) {
        setError('No valid schedules matched that request. Try naming a required class or tightening the time window.');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to generate schedule options.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <section className={styles.panel}>
      <div className={styles.hero}>
        <div>
          <p className={styles.eyebrow}>Smart Enrollment Studio</p>
          <h2>Describe the schedule you want</h2>
          <p>
            Type your ideal week in plain language. The planner interprets the request, searches the
            catalog, and returns candidate schedules for the main calendar preview.
          </p>
        </div>
        <div className={styles.meta}>
          <span>{response?.plannerMode ?? 'Planner ready'}</span>
          <span>{response ? `${response.catalogSize} classes searched` : 'Database-backed search'}</span>
          <span>{response ? `${response.candidates.length} options returned` : 'Awaiting prompt'}</span>
        </div>
      </div>

      <div className={styles.promptCard}>
        <label className={styles.promptField} htmlFor="smart-enrollment-prompt">
          <span>Prompt</span>
          <textarea
            id="smart-enrollment-prompt"
            value={prompt}
            onChange={(event) => setPrompt(event.target.value)}
            placeholder="Example: I need CSCE331 and MATH200, want one design elective, no Friday classes, and I need to be done by 3pm."
            rows={6}
          />
        </label>

        <div className={styles.exampleRow}>
          {PROMPT_EXAMPLES.map((example) => (
            <button key={example} type="button" className={styles.exampleChip} onClick={() => setPrompt(example)}>
              {example}
            </button>
          ))}
        </div>

        <div className={styles.actions}>
          <button type="button" onClick={() => void handleGenerate()} disabled={loading || !prompt.trim()}>
            {loading ? 'Generating options…' : 'Generate schedule options'}
          </button>
          <button
            type="button"
            className={styles.secondaryAction}
            onClick={() => {
              setPrompt('');
              setResponse(null);
              setSelectedCandidateId('');
              setError(null);
              onPreviewCandidate(null);
            }}
            disabled={loading && !response}
          >
            Reset planner
          </button>
        </div>
      </div>

      {error && <div className={styles.error}>{error}</div>}

      {response && (
        <div className={styles.summaryGrid}>
          <section className={styles.summaryCard}>
            <p className={styles.summaryLabel}>Planner summary</p>
            <h3>{response.preferences.summary || 'Schedule intent captured'}</h3>
            <div className={styles.summaryPills}>
              {response.preferenceSummary.map((item) => (
                <span key={item}>{item}</span>
              ))}
            </div>
          </section>

          <section className={styles.summaryCard}>
            <p className={styles.summaryLabel}>Selected preview</p>
            {selectedCandidate ? (
              <>
                <h3>{selectedCandidate.summary}</h3>
                <p>{selectedCandidate.rationale}</p>
                <div className={styles.summaryPills}>
                  {selectedCandidate.highlights.map((item) => (
                    <span key={item}>{item}</span>
                  ))}
                </div>
              </>
            ) : (
              <>
                <h3>No preview selected</h3>
                <p>Generate options to preview a candidate schedule on the calendar.</p>
              </>
            )}
          </section>
        </div>
      )}

      {response && response.candidates.length > 0 && (
        <div className={styles.candidateList}>
          {response.candidates.map((candidate) => {
            const active = candidate.id === selectedCandidate?.id;
            return (
              <article key={candidate.id} className={active ? styles.candidateActive : styles.candidate}>
                <div className={styles.candidateHeader}>
                  <div>
                    <strong>{candidate.summary}</strong>
                    <p>{candidate.rationale}</p>
                  </div>
                  <div className={styles.candidateActions}>
                    <button
                      type="button"
                      className={active ? styles.previewButtonActive : styles.previewButton}
                      onClick={() => setSelectedCandidateId(candidate.id)}
                    >
                      {active ? 'Previewing' : 'Preview on calendar'}
                    </button>
                    <button
                      type="button"
                      onClick={async () => {
                        setSelectedCandidateId(candidate.id);
                        setAccepting(true);
                        try {
                          await onAcceptCandidate(candidate.scheduledClasses);
                        } finally {
                          setAccepting(false);
                        }
                      }}
                      disabled={accepting}
                    >
                      {accepting && active ? 'Applying…' : 'Apply schedule'}
                    </button>
                  </div>
                </div>

                <div className={styles.summaryPills}>
                  {candidate.highlights.map((item) => (
                    <span key={item}>{item}</span>
                  ))}
                </div>

                <div className={styles.classList}>
                  {candidate.scheduledClasses.map((item) => (
                    <div key={item.classId} className={styles.classCard}>
                      <strong>{item.classId}</strong>
                      <span>{item.title}</span>
                      <span>
                        {item.days.join('/')} {item.startTime}-{item.endTime}
                      </span>
                      <span>
                        {item.instructor} • {item.location ?? item.room} • {item.credits} credits
                      </span>
                    </div>
                  ))}
                </div>
              </article>
            );
          })}
        </div>
      )}
    </section>
  );
}
