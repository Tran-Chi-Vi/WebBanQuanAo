/* ==========================================================================
   WEBBANQUANAO - React Powered AI Chatbot Widget Component
   ========================================================================== */

const { useState, useEffect, useRef } = React;

function ChatbotWidget() {
  const [isOpen, setIsOpen] = useState(false);
  const [messages, setMessages] = useState([
    {
      sender: 'bot',
      text: 'Xin chào! Tôi là Chuyên Viên Tư Vấn Thời Trang & Hậu Mãi AI của FASHION STORE 👑.\nTôi có thể hỗ trợ bạn chọn size chuẩn, tư vấn phối đồ, tra cứu đơn hàng hoặc giải đáp chính sách đổi trả!',
      data: null
    }
  ]);
  const [inputMsg, setInputMsg] = useState('');
  const [loading, setLoading] = useState(false);
  const chatEndRef = useRef(null);

  const quickPills = [
    "📏 Tư vấn chọn Size",
    "🔄 Chính sách đổi trả",
    "🚚 Phí giao hàng",
    "📦 Tra cứu đơn hàng",
    "🎁 Ưu đãi & Mã giảm giá"
  ];

  const scrollToBottom = () => {
    chatEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    if (isOpen) scrollToBottom();
  }, [messages, isOpen]);

  const sendQueryText = async (textToSend) => {
    if (!textToSend.trim() || loading) return;
    const userText = textToSend.trim();
    setInputMsg('');
    setMessages(prev => [...prev, { sender: 'user', text: userText }]);
    setLoading(true);

    try {
      const res = await fetch('/Chatbot/SendMessage', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message: userText })
      });
      const data = await res.json();
      setMessages(prev => [...prev, { sender: 'bot', text: data.reply, data: data.data }]);
    } catch (err) {
      setMessages(prev => [...prev, { sender: 'bot', text: 'Rất tiếc, đã có lỗi kết nối. Vui lòng thử lại sau.' }]);
    } finally {
      setLoading(false);
    }
  };

  const handleSend = (e) => {
    e?.preventDefault();
    sendQueryText(inputMsg);
  };

  return (
    <div style={{ position: 'fixed', bottom: '20px', right: '20px', zIndex: 99999 }}>
      {/* Floating Toggle Button */}
      {!isOpen && (
        <button
          onClick={() => setIsOpen(true)}
          className="btn btn-primary-gradient shadow-lg d-flex align-items-center justify-content-center border-0"
          style={{ width: '48px', height: '48px', minWidth: '48px', minHeight: '48px', borderRadius: '50%', padding: '0', fontSize: '1.3rem', aspectRatio: '1/1', flexShrink: 0 }}
          title="Chuyên Viên AI Tư Vấn Trực Tuyến"
        >
          <i className="bi bi-chat-dots-fill"></i>
        </button>
      )}

      {/* Chat Window Container */}
      {isOpen && (
        <div
          id="aiChatWindow"
          className="card shadow-2xl border-0 animate__animated animate__fadeInUp ai-chat-window-card"
          style={{
            width: '340px',
            height: '460px',
            borderRadius: '20px',
            overflow: 'hidden',
            display: 'flex',
            flexDirection: 'column'
          }}
        >
          {/* Chat Header */}
          <div
            className="p-3 text-white d-flex align-items-center justify-content-between flex-shrink-0"
            style={{ background: 'linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%)' }}
          >
            <div className="d-flex align-items-center gap-2">
              <div className="bg-white text-primary rounded-circle p-2 d-flex align-items-center justify-content-center shadow-sm" style={{ width: 38, height: 38, minWidth: 38, minHeight: 38, flexShrink: 0, aspectRatio: '1/1' }}>
                <i className="bi bi-robot fs-5"></i>
              </div>
              <div>
                <h6 className="m-0 font-heading fw-bold" style={{ fontSize: '0.95rem' }}>Fashion AI Assistant</h6>
                <small className="opacity-75" style={{ fontSize: '0.75rem' }}>● Chuyên viên tư vấn & hậu mãi</small>
              </div>
            </div>
            <button onClick={() => setIsOpen(false)} class="btn-close btn-close-white" style={{ outline: 'none', boxShadow: 'none' }}></button>
          </div>

          {/* Chat Body */}
          <div id="aiChatMessages" className="p-3 flex-grow-1 overflow-auto ai-chat-body" style={{ height: '320px' }}>
            {messages.map((m, idx) => (
              <div key={idx} className={`d-flex mb-3 ${m.sender === 'user' ? 'justify-content-end' : 'justify-content-start'}`}>
                <div
                  className={`p-3 rounded-4 shadow-sm ai-msg-bubble ${
                    m.sender === 'user'
                      ? 'bg-primary text-white user-msg'
                      : 'bot-msg'
                  }`}
                  style={{ maxWidth: '85%', fontSize: '0.88rem', lineHeight: '1.5' }}
                >
                  <p className="m-0" style={{ whiteSpace: 'pre-line' }}>{m.text}</p>
                  
                  {/* Render Product Suggestion Cards */}
                  {m.data && Array.isArray(m.data) && (
                    <div className="mt-2.5 d-flex flex-column gap-2">
                      {m.data.map(p => (
                        <a
                          key={p.id}
                          href={p.url}
                          className="d-flex align-items-center gap-2 p-2 rounded-3 text-decoration-none border ai-product-card-link"
                          style={{ transition: 'all 0.2s ease' }}
                        >
                          <img src={p.image} alt={p.name} style={{ width: 44, height: 44, objectFit: 'cover', borderRadius: 8, flexShrink: 0 }} />
                          <div style={{ flex: 1, minWidth: 0 }}>
                            <div className="fw-bold text-truncate" style={{ fontSize: '0.82rem' }}>{p.name}</div>
                            <div className="text-primary fw-bold" style={{ fontSize: '0.8rem' }}>{p.price}</div>
                          </div>
                          <i className="bi bi-chevron-right text-muted" style={{ fontSize: '0.8rem' }}></i>
                        </a>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            ))}
            {loading && (
              <div className="d-flex align-items-center gap-2 text-muted small ms-2 mb-2">
                <div className="spinner-grow spinner-grow-sm text-primary" role="status"></div>
                <span>Đang tra cứu dữ liệu...</span>
              </div>
            )}
            <div ref={chatEndRef} />
          </div>

          {/* Quick Suggestion Pills */}
          <div className="px-2 py-1.5 border-top d-flex gap-1.5 overflow-auto flex-shrink-0 ai-pills-bar" style={{ whiteSpace: 'nowrap', scrollbarWidth: 'none' }}>
            {quickPills.map((pill, i) => (
              <button
                key={i}
                type="button"
                onClick={() => sendQueryText(pill.replace(/^[^\s]+\s/, ''))}
                className="btn btn-sm btn-light rounded-pill px-2.5 py-1 text-muted border small ai-suggestion-pill"
                style={{ fontSize: '0.74rem', flexShrink: 0 }}
              >
                {pill}
              </button>
            ))}
          </div>

          {/* Chat Input Footer */}
          <form onSubmit={handleSend} className="p-2.5 border-top d-flex align-items-center gap-2 flex-shrink-0 ai-input-container">
            <input
              type="text"
              id="aiMessageInput"
              className="form-control border-0 px-3 rounded-pill ai-input-field"
              placeholder="Nhập câu hỏi hoặc từ khóa..."
              value={inputMsg}
              onChange={e => setInputMsg(e.target.value)}
              style={{ fontSize: '0.88rem', height: '40px' }}
            />
            <button
              type="submit"
              id="aiSendBtn"
              className="btn btn-primary-gradient rounded-circle d-flex align-items-center justify-content-center border-0 flex-shrink-0 shadow-sm ai-send-button"
              style={{ width: '42px', height: '42px', minWidth: '42px', minHeight: '42px', maxWidth: '42px', maxHeight: '42px', borderRadius: '50%', padding: '0', aspectRatio: '1/1' }}
              title="Gửi câu hỏi"
            >
              <i className="bi bi-send-fill" style={{ fontSize: '0.95rem' }}></i>
            </button>
          </form>
        </div>
      )}
    </div>
  );
}

// Render React Chatbot Widget into container
const rootElem = document.getElementById('react-chatbot-root');
if (rootElem) {
  const root = ReactDOM.createRoot(rootElem);
  root.render(<ChatbotWidget />);
}
