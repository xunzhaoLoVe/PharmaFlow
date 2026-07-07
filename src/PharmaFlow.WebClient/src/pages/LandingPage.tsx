import { useState } from 'react';
import { Button, Dropdown, Tooltip } from 'antd';
import {
  ArrowRightOutlined,
  BarChartOutlined,
  CheckOutlined,
  CloudSyncOutlined,
  DatabaseOutlined,
  DownOutlined,
  LoginOutlined,
  MoonOutlined,
  SafetyCertificateOutlined,
  SunOutlined,
  UserSwitchOutlined,
} from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';

const pillItems = [
  { label: '多源数据归集', icon: <CloudSyncOutlined /> },
  { label: '渠道风险校验', icon: <SafetyCertificateOutlined /> },
  { label: '主数据标准化', icon: <UserSwitchOutlined /> },
];

const featureCards = [
  {
    title: '采集同步',
    desc: '采集汇总中的采购流向数据同步进入业务管理，保留处理状态。',
    icon: <DatabaseOutlined />,
    tone: 'blue',
  },
  {
    title: '客户治理',
    desc: '经销商、终端客户分层管理，重复客户名称不重复统计。',
    icon: <UserSwitchOutlined />,
    tone: 'green',
  },
  {
    title: '报表分析',
    desc: '首页仪表盘与报表统计跟随药品、客户、采购、销售数据一致更新。',
    icon: <BarChartOutlined />,
    tone: 'cyan',
  },
];

export default function LandingPage() {
  const navigate = useNavigate();
  const [language, setLanguage] = useState(() => localStorage.getItem('language') === 'en' ? 'en' : 'zh');
  const [darkMode, setDarkMode] = useState(() => localStorage.getItem('themeMode') === 'dark');
  const languageMeta = language === 'zh' ? { country: 'CN', code: 'ZH' } : { country: 'US', code: 'EN' };
  const languageMenuItems = [
    {
      key: 'zh',
      label: (
        <span className="public-language-menu-item">
          <span>CN</span>
          <span>中文</span>
          {language === 'zh' && <CheckOutlined />}
        </span>
      ),
    },
    {
      key: 'en',
      label: (
        <span className="public-language-menu-item">
          <span>US</span>
          <span>English</span>
          {language === 'en' && <CheckOutlined />}
        </span>
      ),
    },
  ];

  const handleLanguageChange = ({ key }: { key: string }) => {
    const nextLanguage = key === 'en' ? 'en' : 'zh';
    setLanguage(nextLanguage);
    localStorage.setItem('language', nextLanguage);
  };

  const handleThemeMode = () => {
    const nextMode = !darkMode;
    setDarkMode(nextMode);
    localStorage.setItem('themeMode', nextMode ? 'dark' : 'light');
  };

  return (
    <main className={`landing-shell ${darkMode ? 'is-public-dark' : ''}`}>
      <header className="landing-topbar">
        <div className="landing-nav-inner">
          <button type="button" className="public-brand-button" onClick={() => navigate('/')} aria-label="PharmaFlow">
            <span className="public-brand-mark" aria-hidden="true">
              <span />
              <span />
              <span />
            </span>
          </button>

          <div className="public-top-actions">
            <Dropdown
              menu={{ items: languageMenuItems, onClick: handleLanguageChange }}
              placement="bottomRight"
              trigger={['click']}
              overlayClassName="public-language-dropdown"
            >
              <button type="button" className="public-language-button" aria-label="切换语言">
                <span>{languageMeta.country}</span>
                <span>{languageMeta.code}</span>
                <DownOutlined />
              </button>
            </Dropdown>
            <span className="public-top-divider" />
            <Tooltip title={darkMode ? '浅色模式' : '深色模式'}>
              <button
                type="button"
                className={`public-icon-button ${darkMode ? 'is-active' : ''}`}
                onClick={handleThemeMode}
                aria-label={darkMode ? '浅色模式' : '深色模式'}
              >
                {darkMode ? <SunOutlined /> : <MoonOutlined />}
              </button>
            </Tooltip>
            <button type="button" className="public-login-button" onClick={() => navigate('/login')}>
              <LoginOutlined />
              登录
            </button>
          </div>
        </div>
      </header>

      <section className="landing-hero">
        <div className="landing-hero-copy">
          <h1>药品流向数据治理平台</h1>
          <p>面向药企渠道经营，统一管理采集、客户、采购、销售与报表数据。</p>
          <Button type="primary" size="large" className="landing-start" onClick={() => navigate('/login')}>
            进入工作台
            <ArrowRightOutlined />
          </Button>
        </div>
      </section>

      <section className="landing-pills" aria-label="平台能力">
        {pillItems.map((item) => (
          <span className="landing-pill" key={item.label}>
            <span className="landing-pill-icon">{item.icon}</span>
            <span className="landing-pill-label">{item.label}</span>
          </span>
        ))}
      </section>

      <section className="landing-features" aria-label="核心能力">
        {featureCards.map((card) => (
          <article className="landing-feature-card" data-tone={card.tone} key={card.title}>
            <span className="landing-feature-icon">{card.icon}</span>
            <div className="landing-feature-copy">
              <h2>{card.title}</h2>
              <p>{card.desc}</p>
            </div>
          </article>
        ))}
      </section>
    </main>
  );
}
